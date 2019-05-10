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

from __future__ import print_function

import json
import sys
import time
import inspect
import importlib

HEADER = "Content-Length: "
HEADER_LEN = len(HEADER)


def read_request():
    content_length = None

    # Read headers
    while True:
        line = sys.stdin.readline()
        if not line.rstrip("\r\n"):
            break

        if line.startswith(HEADER):
            content_length = int(line[HEADER_LEN:])

    # Content-Length is required
    assert content_length

    data = sys.stdin.read(content_length)
    return json.loads(data)


def write_response(id, result):
    # TODO: errors?
    s = json.dumps({"jsonrpc": "2.0", "id": id, "result": result})

    data = "Content-Length: {}\r\n\r\n".format(len(s)) + s
    sys.stdout.buffer.write(bytes(data, "utf-8"))
    sys.stdout.buffer.flush()


class Mux(object):
    def __init__(self):
        self.handlers = {}

    def handler(self, method):
        def decorator(func):
            self.handlers[method] = func
            return func

        return decorator

    def handle(self, request):
        handler = self.handlers.get(request["method"], lambda request: None)
        result = handler(*request["params"])
        write_response(request["id"], result)


mux = Mux()


def do_not_inspect(v):
    # https://github.com/Microsoft/python-language-server/issues/740
    # https://github.com/cython/cython/issues/1470
    if type(v).__name__ != "fused_cython_function":
        return False

    # If a fused function has __defaults__, then attempting to access
    # __kwdefaults__ will fail if generated before cython 0.29.6.
    return bool(getattr(v, "__defaults__", False))


@mux.handler("moduleMembers")
def module_members(module_name):
    try:
        module = importlib.import_module(module_name)
        members = inspect.getmembers(module)
        return [name for name, _ in members]
    except:
        # TODO: Don't do this; return an error over RPC.
        return None

def main():
    while True:
        request = read_request()
        mux.handle(request)


if __name__ == "__main__":
    main()
