# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
#
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABILITY OR NON-INFRINGEMENT.
#
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

# Supported Python versions: 2.7, 3.5+
# https://devguide.python.org/#status-of-python-branches

from __future__ import print_function

import json
import sys
import time
import inspect
import importlib

sys.stderr = open("C:\\Users\\jabaile\\Desktop\\log.txt", "a")

if sys.version_info >= (3,):
    stdout = sys.stdout.buffer
else:
    stdout = sys.stdout

    if sys.platform == "win32":
        import os, msvcrt

        msvcrt.setmode(sys.stdout.fileno(), os.O_BINARY)


def write_stdout(data):
    if not isinstance(data, bytes):
        data = data.encode("utf-8")

    stdout.write(data)
    stdout.flush()


METHOD_NOT_FOUND = -32601
INTERNAL_ERROR = -32603


def read_request():
    headers = dict()

    # Read headers
    while True:
        line = sys.stdin.readline().rstrip("\r\n")
        if not line:
            break

        key, _, value = line.partition(":")
        headers[key] = value

    try:
        length = int(headers["Content-Length"])
    except (KeyError, ValueError):
        raise IOError("Content-Length is missing or invalid")

    body = ""
    while length > 0:
        chunk = sys.stdin.read(length)
        body += chunk
        length -= len(chunk)

    request = json.loads(body)
    return Request(request)


def write_response(id, result=None, error=None):
    response = {"jsonrpc": "2.0", "id": id}

    if result is not None:
        response["result"] = result

    if error is not None:
        assert result is None
        response["error"] = error

    s = json.dumps(response)

    data = "Content-Length: {}\r\n\r\n".format(len(s)) + s
    write_stdout(data)


class Request(object):
    def __init__(self, request):
        self.id = request["id"]
        self.method = request["method"]
        self.params = request.get("params", None)

    def write_result(self, result):
        write_response(self.id, result)

    def write_error(self, code, message):
        write_response(self.id, error={"code": code, "message": message})


class Mux(object):
    def __init__(self):
        self.handlers = {}

    def handler(self, method):
        def decorator(func):
            self.handlers[method] = func
            return func

        return decorator

    def handle(self, request):
        handler = self.handlers.get(request.method, None)

        if not handler:
            request.write_error(
                METHOD_NOT_FOUND, "method {} not found".format(request.method)
            )
            return

        try:
            result = handler(*request.params)
        except Exception as e:
            request.write_error(result, INTERNAL_ERROR, str(e))
        else:
            request.write_result(result)


mux = Mux()


def do_not_inspect(v):
    # https://github.com/Microsoft/python-language-server/issues/740
    # https://github.com/cython/cython/issues/1470
    if type(v).__name__ != "fused_cython_function":
        return False

    # If a fused function has __defaults__, then attempting to access
    # __kwdefaults__ will fail if generated before cython 0.29.6.
    return bool(getattr(v, "__defaults__", False))


@mux.handler("moduleMemberNames")
def module_members(module_name):
    module = importlib.import_module(module_name)
    members = inspect.getmembers(module)
    return [name for name, _ in members]


def main():
    while True:
        request = read_request()
        mux.handle(request)


if __name__ == "__main__":
    main()
