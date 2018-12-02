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
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

from __future__ import print_function

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "15.8"

import ast
import keyword
import inspect
import io
import re
import sys
import tokenize
import warnings

try:
    import builtins
except ImportError:
    import __builtin__ as builtins

try:
    bytes
except NameError:
    bytes = str

try:
    unicode
except NameError:
    unicode = str

class InspectWarning(UserWarning): pass

def _triple_quote(s):
    if "'" not in s:
        return "'''" + s + "'''"
    if '"' not in s:
        return '"""' + s + '"""'
    if not s.startswith("'"):
        return "'''" + s.replace("'''", "\\'\\'\\'") + " '''"
    if not s.startswith('"'):
        return '"""' + s.replace('"""', '\\"\\"\\"') + ' """'
    return "''' " + s.replace("'''", "\\'\\'\\'") + " '''"

try:
    EXACT_TOKEN_TYPES = tokenize.EXACT_TOKEN_TYPES
except AttributeError:
    # Bare minimum that we need here
    EXACT_TOKEN_TYPES = {
        '(': tokenize.LPAR,
        ')': tokenize.RPAR,
        '[': tokenize.LSQB,
        ']': tokenize.RSQB,
        '{': tokenize.LBRACE,
        '}': tokenize.RBRACE,
        ',': tokenize.COMMA,
        ':': tokenize.COLON,
        '*': tokenize.STAR,
        '**': tokenize.DOUBLESTAR,
        '=': tokenize.EQUAL,
    }

SKIP_TYPENAME_FOR_TYPES = bool, str, bytes, int, float
STATICMETHOD_TYPES = ()
CLASSMETHOD_TYPES = type(float.fromhex),
PROPERTY_TYPES = type(int.real), type(property.fget)

INVALID_ARGNAMES = set(keyword.kwlist) | set(('None', 'True', 'False'))

# These full names are known to be lies. When we encounter
# them while scraping a module, assume that we need to write
# out the full type rather than including them by reference.
LIES_ABOUT_MODULE = frozenset([
    builtins.__name__ + ".weakcallableproxy",
    builtins.__name__ + ".weakproxy",
    builtins.__name__ + ".weakref",
    "ctypes.ArgumentError",
    "os.stat_result",
    "os.statvfs_result",
    "xml.parsers.expat.ExpatError",

    "numpy.broadcast",
    "numpy.busdaycalendar",
    "numpy.dtype",
    "numpy.flagsobj",
    "numpy.flatiter",
    "numpy.ndarray",
    "numpy.nditer",

    # These modules contain multiple members that lie about their
    # module. Always write out all members of these in full.
    "_asyncio.*",
    "_bsddb.*",
    "_decimal.*",
    "_elementtree.*",
    "_socket.*",
    "_sqlite3.*",
    "_ssl.*",
    "_testmultiphase.*",
])

# These type names cause conflicts with their values, so
# we need to forcibly rename them.
SYS_INFO_TYPES = frozenset((
    "float_info",
    "hash_info",
    "int_info",
    "thread_info",
    "version_info",
))

VALUE_REPR_FIX = {
    float('inf'): "float('inf')",
    float('-inf'): "float('-inf')",
}

IMPLICIT_CLASSMETHOD = (
    '__new__',
)

if sys.version_info[0] < 3:
    SKIP_TYPENAME_FOR_TYPES += unicode, long

def safe_callable(v):
    try:
        return hasattr(v, '__call__')
    except Exception:
        return False

def safe_module_name(n):
    if n:
        return '_mod_' + n.replace('.', '_')
    return n

class Signature(object):
    # These two dictionaries start with Python 3 values.
    # There is an update below for Python 2 differences.
    # They will be used as fallbacks for known protocols

    KNOWN_RESTYPES = {
        "__abs__": "__T__()",
        "__add__": "__T__()",
        "__and__": "__T__()",
        "__annotations__": "{}",
        "__base__": "type",
        "__bases__": "(type,)",
        "__bool__": "False",
        "__call__": "Any",
        "__ceil__": "__T__()",
        "__code__": "object()",
        "__contains__": "False",
        "__del__": "None",
        "__delattr__": "None",
        "__delitem__": "None",
        "__dict__": "{'': Any}",
        "__dir__": "['']",
        "__divmod__": "(0, 0)",
        "__eq__": "False",
        "__format__": "''",
        "__float__": "0.0",
        "__floor__": "__T__()",
        "__floordiv__": "0",
        "__ge__": "False",
        "__get__": "__T__()",
        "__getattribute__": "Any",
        "__getitem__": "Any",
        "__getnewargs__": "()",
        "__getnewargs_ex__": "((), {})",
        "__getslice__": "__T__()",
        "__globals__": "{}",
        "__gt__": "False",
        "__hash__": "0",
        "__iadd__": "None",
        "__iand__": "None",
        "__imul__": "None",
        "__index__": "0",
        "__init__": "",
        "__init_subclass__": "None",
        "__int__": "0",
        "__invert__": "__T__()",
        "__ior__": "None",
        "__isub__": "None",
        "__iter__": "__T__()",
        "__ixor__": "None",
        "__le__": "False",
        "__len__": "0",
        "__length_hint__": "0",
        "__lshift__": "__T__()",
        "__lt__": "False",
        "__mod__": "__T__()",
        "__mul__": "__T__()",
        "__ne__": "False",
        "__neg__": "__T__()",
        "__next__": "Any",
        "__pos__": "__T__()",
        "__pow__": "__T__()",
        "__or__": "__T__()",
        "__radd__": "__T__()",
        "__rand__": "__T__()",
        "__rdivmod__": "(0, 0)",
        "__rfloordiv__": "__T__()",
        "__rlshift__": "__T__()",
        "__rmod__": "__T__()",
        "__rmul__": "__T__()",
        "__ror__": "__T__()",
        "__round__": "__T__()",
        "__rpow__": "__T__()",
        "__rrshift__": "__T__()",
        "__rshift__": "__T__()",
        "__rsub__": "__T__()",
        "__rtruediv__": "__T__()",
        "__rxor__": "__T__()",
        "__reduce__": ["''", "()"],
        "__reduce_ex__": ["''", "()"],
        "__repr__": "''",
        "__set__": "None",
        "__setattr__": "None",
        "__setitem__": "None",
        "__setstate__": "None",
        "__sizeof__": "0",
        "__str__": "''",
        "__sub__": "__T__()",
        "__truediv__": "0.0",
        "__trunc__": "__T__()",
        "__xor__": "__T__()",
        "__subclasscheck__": "False",
        "__subclasshook__": "False",
    }

    KNOWN_ARGSPECS = {
        "__contains__": "(self, value)",
        "__del__": "(self)",
        "__dir__": "(self)",
        "__floor__": "(self)",
        "__format__": "(self, format_spec)",
        "__getitem__": "(self, index)",
        "__getnewargs__": "(self)",
        "__getnewargs_ex__": "(self)",
        "__init_subclass__": "(cls)",
        "__instancecheck__": "(self, instance)",
        "__length_hint__": "(self)",
        "__prepare__": "(cls, name, bases, **kwds)",
        "__round__": "(self, ndigits=0)",
        "__reduce__": "(self)",
        "__reduce_ex__": "(self, protocol)",
        "__reversed__": "(self)",
        "__setitem__": "(self, index, value)",
        "__setstate__": "(self, state)",
        "__sizeof__": "(self)",
        "__subclasses__": "(cls)",
        "__subclasscheck__": "(cls, subclass)",
        "__subclasshook__": "(cls, subclass)",
        "__trunc__": "(self)",
    }


    def __init__(self,
        name,
        callable,
        scope=None,
        defaults=None,
        scope_alias=None,
        decorators=None,
        module_doc=None,
    ):
        self.callable = callable
        self.name = name
        self.scope = scope
        self.decorators = decorators or ()
        self._signature = None
        self._defaults = defaults or ()

        if scope and '@staticmethod' not in self.decorators:
            def_arg = 'cls' if ('@classmethod' in self.decorators or name in IMPLICIT_CLASSMETHOD) else 'self'
            if len(self._defaults) == 0 or self._defaults[0] != def_arg:
                self._defaults = (def_arg,) + self._defaults
        
        self.fullsig = None
        if self.name in ('__init__', '__new__') and module_doc:
            self.fullsig = self._init_argspec_fromdocstring(self._defaults, module_doc, override_name=self.name)
        elif not hasattr(self.callable, '__call__') and hasattr(self.callable, '__get__'):
            # We have a property
            self.decorators = '@property',
            self.fullsig = self.name + "(" + ", ".join(self._defaults) + ")"
        
        self.fullsig = (
            self.fullsig or
            # Disable fromsignature() because it doesn't work as well as argspec
            #self._init_argspec_fromsignature(self._defaults) or
            self._init_argspec_fromargspec(self._defaults) or
            self._init_argspec_fromknown(self._defaults, scope_alias) or
            self._init_argspec_fromdocstring(self._defaults) or
            (self.name + "(" + ", ".join(self._defaults) + ")")
        )
        self.restype = (
            self._init_restype_fromsignature() or
            self._init_restype_fromknown(scope_alias) or
            self._init_restype_fromdocstring() or
            'pass'
        )

        if scope:
            self.restype = self.restype.replace('__T__', scope)

        if self.restype in ('return Any', 'return Unknown'):
            self.restype = 'pass'

        #Special case for 'with' statement and built-ins like open() or memoryview
        if name == '__enter__' and self.restype == 'pass':
            self.restype = 'return self'

    def __str__(self):
        return self.fullsig

    def _init_argspec_fromsignature(self, defaults):
        try:
            sig = inspect.signature(self.callable)
        except Exception:
            return

        new_args = []
        for arg in sig.parameters:
            p = sig.parameters[arg]
            if p.default != inspect.Signature.empty:
                try:
                    ast.literal_eval(repr(p.default))
                except Exception:
                    p = p.replace(default=None)
            if p.kind == inspect.Parameter.POSITIONAL_ONLY:
                p = p.replace(kind=inspect.Parameter.POSITIONAL_OR_KEYWORD)
            new_args.append(p)
        sig = sig.replace(parameters=new_args)

        return self.name + str(sig)

    def _init_restype_fromsignature(self):
        try:
            sig = inspect.signature(self.callable)
        except Exception:
            return

        # If signature has a return annotation, it's in the
        # full signature and we don't need it from here.
        if not sig or sig.return_annotation == inspect._empty:
            return
        ann = inspect.formatannotation(sig.return_annotation)
        if not ann or not self._can_eval(ann):
            return
        return 'return ' + ann

    def _init_argspec_fromargspec(self, defaults):
        try:
            args = (getattr(inspect, 'getfullargspec', None) or inspect.getargspec)(self.callable)
        except Exception:
            return

        argn = []
        seen_names = set(INVALID_ARGNAMES)
        defaults = list(defaults)
        default_set = set(defaults)
        for a in args.args:
            if a in default_set:
                default_set.discard(a)
            argn.append(self._make_unique_name(a, seen_names))
        if default_set:
            argn[:0] = [a for a in defaults if a in default_set]

        if getattr(args, 'varargs', None):
            argn.append('*' + args.varargs)
        if getattr(args, 'varkw', None):
            argn.append('**' + args.varkw)

        if argn and argn[-1] in ('*', '**'):
            argn[-1] += self._make_unique_name('_', seen_names)

        return self.name + '(' + ', '.join(argn) + ')'

    def _init_argspec_fromknown(self, defaults, scope_alias):
        spec = None
        if scope_alias and not spec:
            spec = self.KNOWN_ARGSPECS.get(scope_alias + '.' + self.name)
        if self.scope and not spec:
            spec = self.KNOWN_ARGSPECS.get(self.scope + '.' + self.name)
        if not spec:
            spec = self.KNOWN_ARGSPECS.get(self.name)
        if not spec:
            return

        return self.name + spec

    def _init_restype_fromknown(self, scope_alias):
        restype = None
        if scope_alias and not restype:
            restype = self.KNOWN_RESTYPES.get(scope_alias + '.' + self.name)
        if self.scope and not restype:
            restype = self.KNOWN_RESTYPES.get(self.scope + '.' + self.name)
        if not restype:
            restype = self.KNOWN_RESTYPES.get(self.name)
        if not restype:
            return

        if isinstance(restype, list):
            return "return " + "; return ".join(restype)
        return "return " + restype

    def _init_restype_fromdocstring(self):
        doc = getattr(self.callable, '__doc__', None)
        if not isinstance(doc, str):
            return
        
        first_line = doc.partition('\n')[0].strip()
        if not '->' in first_line:
            return

        index = first_line.index('->')
        typeName = first_line[index + 2:].strip()
        
        if typeName.startswith('str'):
            return "return ''"
        if typeName.startswith('float'):
            return "return 1.0"
        if typeName.startswith('int'):
            return "return 1"
        if typeName.startswith('long'):
            if sys.version_info[0] < 3:
                return "return 1L"
            else:
                return "return 1"
        if typeName.startswith('list'):
            return "return list()"
        if typeName.startswith('dict'):
            return "return dict()"
        if typeName.startswith('('):
            return "return tuple()"
        if typeName.startswith('bool'):
            return "return True"
        if 'Return a string' in first_line:
            return "return ''"
        return

    def _init_argspec_fromdocstring(self, defaults, doc=None, override_name=None):
        if not doc:
            doc = getattr(self.callable, '__doc__', None)
        if not isinstance(doc, str):
            return

        doc = self._get_first_function_call(doc)
        if not doc:
            return

        if(override_name):
            allow_name_mismatch = override_name not in doc
        else:
            allow_name_mismatch = False

        return self._parse_funcdef(doc, allow_name_mismatch, defaults, override_name)

    def _make_unique_name(self, name, seen_names):
        if name not in seen_names:
            seen_names.add(name)
            return name

        n = name + '_'
        if n not in seen_names:
            seen_names.add(n)
            return n

        i = 0
        while True:
            i += 1
            n = name + '_' + str(i)
            if n not in seen_names:
                seen_names.add(n)
                return n

        raise RuntimeError("Too many arguments in definition")

    def _tokenize(self, expr):
        if sys.version_info[0] == 3 and sys.version_info[1] <= 2:
            expr = '# coding: utf-8\n' + expr
        buf = io.BytesIO(expr.strip().encode('utf-8'))
        if sys.version_info[0] == 3:
            tokens = tokenize.tokenize(buf.readline)
        else:
            tokens = tokenize.generate_tokens(buf.readline)
        return [(EXACT_TOKEN_TYPES.get(s, tt) if tt == tokenize.OP else tt, s)
                for tt, s, _, _, _ in tokens]

    _PAREN_TOKEN_MAP = {
        tokenize.LPAR: tokenize.RPAR,
        tokenize.LBRACE: tokenize.RBRACE,
        tokenize.LSQB: tokenize.RSQB,
    }

    def _parse_take_expr(self, tokens, *stop_at):
        nesting = []
        expr = []
        while tokens:
            tt, s = tokens[0]
            if tt == tokenize.LSQB and len(tokens) > 2 and tokens[1][0] in stop_at:
                return expr
            if tt in self._PAREN_TOKEN_MAP:
                expr.append((tt, s))
                nesting.append(self._PAREN_TOKEN_MAP[tt])
            elif nesting and tt == nesting[-1]:
                expr.append((tt, s))
                nesting.pop()
            elif tt in (tokenize.RPAR, tokenize.RBRACE, tokenize.RSQB):
                return expr
            elif not nesting and tt in stop_at:
                return expr
            else:
                expr.append((tt, s))
            tokens.pop(0)
        return expr

    def _can_eval(self, s):
        if not s:
            return False
        try:
            ast.parse(s, mode='eval')
        except SyntaxError:
            return False
        else:
            return True

    def _parse_format_arg(self, name, args, defaults):
        defaults = list(defaults)
        default_set = set(defaults)
        seen_names = set(INVALID_ARGNAMES)
        parts = [name or '<function>', '(']
        arg_parts = []
        any_default = False

        for a_names, a_ann, a_def, a_opt in args:
            if not a_names:
                continue
            a_name = ''.join(a_names)
            if a_name in default_set:
                default_set.discard(a_name)

            arg_parts.append(self._make_unique_name(a_name, seen_names))
            if self._can_eval(''.join(a_ann)):
                arg_parts.append(': ')
                arg_parts.extend(a_ann)
            if self._can_eval(''.join(a_def)):
                arg_parts.append('=')
                arg_parts.extend(a_def)
                any_default = True
            elif a_opt[0] or (any_default and '*' not in a_name and '**' not in a_name):
                arg_parts.append('=None')
                any_default = True
            if a_name.startswith('*'):
                any_default = True
            arg_parts.append(', ')

        if default_set:
            for a in defaults:
                if a in default_set:
                    parts.append(a)
                    parts.append(', ')
        parts.extend(arg_parts)
        if parts[-1] == ', ':
            parts.pop()
        if parts and parts[-1] in ('*', '**'):
            parts[-1] += self._make_unique_name('_', seen_names)

        parts.append(')')

        return ''.join(parts)

    def _parse_funcdef(self, expr, allow_name_mismatch, defaults, override_name=None):
        '''Takes a call expression that was part of a docstring
        and parses the AST as if it were a definition. If the parsed
        AST matches the callable we are wrapping, returns the node.
        '''
        try:
            tokens = self._tokenize(expr)
        except (TypeError, tokenize.TokenError):
            warnings.warn('failed to tokenize ' + expr, InspectWarning)
            return

        name = None
        seen_open_paren = False
        args = [([], [], [], [False])]
        optional = False

        while tokens:
            tt, s = tokens.pop(0)
            if tt == tokenize.NAME:
                if name is None:
                    name = s
                elif seen_open_paren:
                    args[-1][0].append(s)
                    args[-1][3][0] = optional
            elif tt in (tokenize.STAR, tokenize.DOUBLESTAR):
                args[-1][0].append(s)
            elif tt == tokenize.COLON:
                e = self._parse_take_expr(tokens, tokenize.EQUAL, tokenize.COMMA)
                args[-1][1].append(''.join(i[1] for i in e))
            elif tt == tokenize.EQUAL:
                e = self._parse_take_expr(tokens, tokenize.COMMA)
                args[-1][2].append(''.join(i[1] for i in e))
            elif tt == tokenize.COMMA:
                args.append(([], [], [], [False]))
            elif tt == tokenize.LSQB:
                optional = True
            elif tt == tokenize.RSQB:
                optional = False
            elif tt == tokenize.LPAR:
                seen_open_paren = True
            elif tt == tokenize.RPAR:
                break
            elif s in ('->', '...'):
                return

        if name and (allow_name_mismatch or name == self.name):
            return self._parse_format_arg(override_name or name, args, defaults)

    def _get_first_function_call(self, expr):
        '''Scans the string for the first closing parenthesis,
        handling nesting, which is the best heuristic we have for
        an example call at the start of the docstring.'''
        if not expr or ')' not in expr:
            return
        found = []
        n = 0
        for line in expr.splitlines():
            found_one = False
            line = line.strip('\r\n\t ')
            if not line:
                break
            for i, c in enumerate(line):
                if c == ')':
                    n -= 1
                    if n == 0:
                        found.append(line[:i + 1])
                        found_one = True
                elif c == '(':
                    n += 1
            
            if not found_one:
                break
        if found:
            found.sort(key=len)
            return found[-1]
        return

class MemberInfo(object):
    NO_VALUE = object()

    def __init__(self, name, value, literal=None, scope=None, module=None, alias=None, module_doc=None, scope_alias=None):
        self.name = name
        self.value = value
        self.literal = literal
        self.members = []
        self.values = []
        self.need_imports = ()
        self.type_name = None
        self.scope_name = None
        self.bases = ()
        self.signature = None
        self.documentation = getattr(value, '__doc__', None)
        self.alias = alias
        if not isinstance(self.documentation, str):
            self.documentation = None

        # Special case for __init__ that refers to class docs
        if self.name == '__init__' and (
            not self.documentation
            or 'See help(type(self))' in self.documentation):
            self.documentation = module_doc

        if self.name:
            self.name = self.name.replace('-', '_')

        value_type = type(value)
        if issubclass(value_type, type):
            self.need_imports, type_name = self._get_typename(value, module)
            if '.' in type_name:
                m, s, n = type_name.rpartition('.')
                self.literal = safe_module_name(m) + s + n
            else:
                self.scope_name = self.type_name = type_name
                self._collect_bases(value, module, self.type_name)

        elif safe_callable(value):
            dec = ()
            if scope:
                if value_type in STATICMETHOD_TYPES:
                    dec += '@staticmethod',
                elif value_type in CLASSMETHOD_TYPES:
                    dec += '@classmethod',
            self.signature = Signature(name, value, scope, scope_alias=scope_alias, decorators=dec, module_doc=module_doc)
        elif value is not None:
            if value_type in PROPERTY_TYPES:
                self.signature = Signature(name, value, scope, scope_alias=scope_alias)
            if value_type not in SKIP_TYPENAME_FOR_TYPES:
                self.need_imports, self.type_name = self._get_typename(value_type, module)
                self._collect_bases(value_type, module, self.type_name)
            if isinstance(value, float) and repr(value) == 'nan':
                self.literal = "float('nan')"
            try:
                self.literal = VALUE_REPR_FIX[value]
            except Exception:
                pass
        elif not self.literal:
            self.literal = 'None'

    def _collect_bases(self, value_type, module, type_name):
        try:
            bases = getattr(value_type, '__bases__', ())
        except Exception:
            pass
        else:
            self.bases = []
            self.need_imports = list(self.need_imports)
            for ni, t in (self._get_typename(b, module) for b in bases):
                if not t:
                    continue
                if t == type_name and module in ni:
                    continue
                self.bases.append(t)
                self.need_imports.extend(ni)

    @classmethod
    def _get_typename(cls, value_type, in_module):
        try:
            type_name = value_type.__name__.replace('-', '_')
            module = getattr(value_type, '__module__', None)

            # Special workaround for Python 2 exceptions lying about their module
            if sys.version_info[0] == 2 and module == 'exceptions' and in_module == builtins.__name__:
                module = builtins.__name__

            # Special workaround for IronPython types that include their module name
            if in_module and type_name.startswith(in_module + '.'):
                type_name = type_name[len(in_module) + 1:]
                module = in_module

            if module and module != '<unknown>':
                if module == in_module:
                    return (module,), type_name

                fullname = module + '.' + type_name

                if in_module and (fullname in LIES_ABOUT_MODULE or (in_module + '.*') in LIES_ABOUT_MODULE):
                    # Treat the type as if it came from the current module
                    return (in_module,), type_name

                return (module,), fullname

            return (), type_name
        except Exception:
            warnings.warn('could not get type of ' + repr(value_type), InspectWarning)
            raise
            return (), None

    def _str_from_literal(self, lit):
        return self.name + ' = ' + lit

    def _str_from_typename(self, type_name):
        mod_name, sep, name = type_name.rpartition('.')
        return self.name + ' = ' + safe_module_name(mod_name) + sep + name + '()'

    def _str_from_value(self, v):
        return self.name + ' = ' + repr(v)

    def _lines_with_members(self):
        if self.bases:
            split_bases = [n.rpartition('.') for n in self.bases]
            bases = ','.join((safe_module_name(n[0]) + n[1] + n[2]) for n in split_bases)
            yield 'class ' + self.name + '(' + bases + '):'
        else:
            yield 'class ' + self.name + ':'
        if self.documentation:
            yield '    ' + repr(self.documentation)
        if self.members:
            for mi in self.members:
                if hasattr(mi, 'documentation') and mi.documentation != None and not isinstance(mi.documentation, str):
                    continue
                if mi is not MemberInfo.NO_VALUE:
                    yield mi.as_str('    ')
        else:
            yield '    pass'
        yield ''

    def _lines_with_signature(self):
        seen_decorators = set()
        for d in self.signature.decorators:
            d = str(d)
            if d not in seen_decorators:
                seen_decorators.add(d)
                yield d
        yield 'def ' + str(self.signature) + ':'
        if self.documentation:
            yield '    ' + repr(self.documentation)
        if self.signature.restype:
            yield '    ' + self.signature.restype
        else:
            yield '    pass'
        yield ''

    def as_str(self, indent=''):
        if self.literal:
            return indent + self._str_from_literal(self.literal)

        if self.members:
            return '\n'.join(indent + s for s in self._lines_with_members())

        if self.signature:
            return '\n'.join(indent + s for s in self._lines_with_signature())

        if self.type_name is not None:
            return indent + self._str_from_typename(self.type_name)

        if self.value is not None:
            return indent + self._str_from_value(self.value)

        return indent + self.name


MODULE_MEMBER_SUBSTITUTE = {
    '__builtins__': MemberInfo('__builtins__', None, literal='{}'),
    '__spec__': None,
    '__loader__': None,
}

CLASS_MEMBER_SUBSTITUTE = {
    '__bases__': MemberInfo('__bases__', None, literal='()'),
    '__mro__': MemberInfo('__mro__', None, literal='()'),
    '__dict__': MemberInfo('__dict__', None, literal='{}'),
    '__doc__': None,
    '__new__': None,
}

class ScrapeState(object):
    def __init__(self, module_name, module=None):
        self.root_module = None
        self.module = module
        self.module_name = module_name

        self.imports = set()
        self.members = []

    def initial_import(self, search_path=None):
        if self.module:
            return

        if search_path:
            sys.path.insert(0, search_path)

        try:
            try:
                mod = __import__(self.module_name)
            except Exception:
                ex_msg = str(sys.exc_info()[1])
                warnings.warn("Working around " + ex_msg, InspectWarning)
                if ex_msg == "This must be an MFC application - try 'import win32ui' first":
                    import win32ui
                elif ex_msg == "Could not find TCL routines" or self.module_name == 'matplotlib.backends._tkagg':
                    if sys.version_info[0] == 2:
                        import Tkinter
                    else:
                        import tkinter
                else:
                    raise
                mod = None
            if not mod:
                # Try the import again, either fixed or without chaining the
                # previous exception.
                mod = __import__(self.module_name)
        finally:
            if search_path:
                del sys.path[0]
        self.root_module = mod

        # __import__ gives us the topmost module. We should generally use
        # getattr() from here to find the child module. However, sometimes
        # this is unsuccessful. So we work backwards through the full name
        # to see what is in sys.modules, then getattr() to go forwards.
        mod_name = self.module_name
        bits = []
        while mod_name and mod_name not in sys.modules:
            mod_name, _, bit = self.module_name.rpartition('.')[0]
            bits.insert(0, bit)

        if mod_name:
            self.root_module = mod = sys.modules[mod_name]
        else:
            bits = self.module_name.split('.')[1:]

        for bit in bits:
            mod = getattr(mod, bit)

        self.module = mod

    def collect_top_level_members(self):
        self._collect_members(self.module, self.members, MODULE_MEMBER_SUBSTITUTE, None)

        if self.module_name == 'sys':
            sysinfo = [m for m in self.members if m.type_name in SYS_INFO_TYPES]
            for m in sysinfo:
                self.members.append(MemberInfo(m.name, None, literal="__" + m.name + "()"))
                m.name = m.scope_name = m.type_name = '__' + m.type_name

        m_names = set(m.name for m in self.members)
        undeclared = []
        for m in self.members:
            if m.value is not None and m.type_name and '.' not in m.type_name and m.type_name not in m_names:
                undeclared.append(MemberInfo(m.type_name, type(m.value), module=self.module_name))

        self.members[:0] = undeclared

    def _should_collect_members(self, member):
        if self.module_name in member.need_imports and member.name == member.type_name:
            return True
        # Support cffi libs
        if member.type_name == builtins.__name__ + '.CompiledLib':
            return True

    def collect_second_level_members(self):
        for mi in self.members:
            if self._should_collect_members(mi):
                substitutes = dict(CLASS_MEMBER_SUBSTITUTE)
                substitutes['__class__'] = MemberInfo('__class__', None, literal=mi.type_name)
                self._collect_members(mi.value, mi.members, substitutes, mi)

                if mi.scope_name != mi.type_name:
                    # When the scope and type names are different, we have a static
                    # class. To emulate this, we add '@staticmethod' decorators to
                    # all members.
                    for mi2 in mi.members:
                        if mi2.signature:
                            mi2.signature.decorators += '@staticmethod',

    def _collect_members(self, mod, members, substitutes, outer_member):
        '''Fills the members attribute with a dictionary containing
        all members from the module.'''
        if not mod:
            raise RuntimeError("failed to import module")
        if mod is MemberInfo.NO_VALUE:
            return

        existing_names = set(m.name for m in members)

        if outer_member:
            scope = outer_member.scope_name
            scope_alias = outer_member.alias
        else:
            scope, scope_alias = None, None

        mod_scope = (self.module_name + '.' + scope) if scope else self.module_name
        mod_doc = getattr(mod, '__doc__', None)
        mro = (getattr(mod, '__mro__', None) or ())[1:]
        for name in dir(mod):
            if keyword.iskeyword(name):
                continue
            try:
                m = substitutes[name]
                if m:
                    members.append(m)
                continue
            except LookupError:
                pass
            try:
                m = substitutes[mod_scope + '.' + name]
                if m:
                    members.append(m)
                continue
            except LookupError:
                pass

            if name in existing_names:
                continue

            try:
                value = getattr(mod, name)
            except AttributeError:
                warnings.warn("attribute " + name + " on " + repr(mod) + " was in dir() but not getattr()", InspectWarning)
            except Exception:
                warnings.warn("error getting " + name + " for " + repr(mod), InspectWarning)
            else:
                if not self._should_add_value(value):
                    continue
                if name != '__init__' and self._mro_contains(mro, name, value):
                    continue
                members.append(MemberInfo(name, value, scope=scope, module=self.module_name, module_doc=mod_doc, scope_alias=scope_alias))

    def _should_add_value(self, value):
        try:
            value_type = type(value)
            mod = getattr(value_type, '__module__', None)
            name = value_type.__name__
        except Exception:
            warnings.warn("error getting typename", InspectWarning)
            return

        if (mod, name) == (builtins.__name__, 'CompiledLib'):
            # Always allow CFFI lib
            return True

        if issubclass(value_type, (type(sys), type(inspect))):
            # Disallow nested modules
            return

        # By default, include all values
        return True

    def _mro_contains(self, mro, name, value):
        for m in mro:
            try:
                mro_value = getattr(m, name)
            except Exception:
                pass
            else:
                if mro_value is value:
                    return True

    def translate_members(self):
        pass

    def dump(self, out):
        imports = set()
        for value in self.members:
            for mod in value.need_imports:
                imports.add(mod)
        imports.discard(self.module_name)

        if imports:
            for mod in sorted(imports):
                print("import " + mod + " as " + safe_module_name(mod), file=out)
            print("", file=out)

        for value in self.members:
            s = value.as_str('')
            try:
                print(s, file=out)
            except TypeError:
                print(repr(s), file=sys.stderr)
                raise

def add_builtin_objects(state):
    Signature.KNOWN_RESTYPES.update({
        "__Type__.__call__": "cls()",
        "__Property__.__delete__": "None",
        "__Float__.__getformat__": "''",
        "__Bytes__.__getitem__": "__T__()",
        "__Unicode__.__getitem__": "__T__()",
        "__Type__.__instancecheck__": "False",
        "__Tuple__.__iter__": "__TupleIterator__()",
        "__List__.__iter__": "__ListIterator__()",
        "__Dict__.__iter__": "__DictKeys__()",
        "__Set__.__iter__": "__SetIterator__()",
        "__FrozenSet__.__iter__": "__SetIterator__()",
        "__Bytes__.__iter__": "__BytesIterator__()",
        "__Unicode__.__iter__": "__UnicodeIterator__()",
        "__BytesIterator__.__next__": "0",
        "__UnicodeIterator__.__next__": "__Unicode__()",
        "__Type__.__prepare__": "None",
        "__List__.__reversed__": "__ListIterator__()",
        "__Float__.__setformat__": "None",
        "__Type__.__subclasses__": "(cls,)",
        "__truediv__": "__Float__()",
        "__Type__.__subclasscheck__": "False",
        "__subclasshook__": "False",
        "all": "False",
        "any": "False",
        "ascii": "''",
        "__Set__.add": "None",
        "__List__.append": "None",
        "__Float__.as_integer_ratio": "(0, 0)",
        "bin": "''",
        "__Int__.bit_length": "0",
        "callable": "False",
        "capitalize": "__T__()",
        "casefold": "__T__()",
        "center": "__T__()",
        "chr": "''",
        "clear": "None",
        "__Generator__.close": "None",
        "conjugate": "__Complex__()",
        "copy": "__T__()",
        "count": "0",
        "__Bytes__.decode": "''",
        "__Property__.deleter": "func",
        "__Set__.difference": "__T__()",
        "__FrozenSet__.difference": "__T__()",
        "__Set__.difference_update": "None",
        "__Set__.discard": "None",
        "divmod": "(0, 0)",
        "__Bytes__.encode": "b''",
        "__Unicode__.encode": "b''",
        "endswith": "False",
        "expandtabs": "__T__()",
        "__List__.extend": "None",
        "find": "0",
        "__Unicode__.format": "__T__()",
        "__Unicode__.format_map": "__T__()",
        "__Bool__.from_bytes": "False",
        "__Int__.from_bytes": "0",
        "__Long__.from_bytes": "__Long__()",
        "__Float__.fromhex": "0.0",
        "__Bytes__.fromhex": "b''",
        "__Dict__.fromkeys": "{}",
        "__Dict__.get": "self[0]",
        "__Property__.getter": "func",
        "format": "''",
        "globals": "__Dict__()",
        "hasattr": "False",
        "hash": "0",
        "hex": "''",
        "id": "0",
        "index": "0",
        "input": "''",
        "__List__.insert": "None",
        "__Set__.intersection": "__T__()",
        "__FrozenSet__.intersection": "__T__()",
        "__Set__.intersection_update": "None",
        "isalnum": "False",
        "isalpha": "False",
        "isdecimal": "False",
        "isdigit": "False",
        "islower": "False",
        "isidentifier": "False",
        "isnumeric": "False",
        "isprintable": "False",
        "isspace": "False",
        "istitle": "False",
        "isupper": "False",
        "__Float__.is_integer": "False",
        "__Set__.isdisjoint": "False",
        "__FrozenSet__.isdisjoint": "False",
        "__DictKeys__.isdisjoint": "False",
        "__DictItems__.isdisjoint": "False",
        "__Set__.issubset": "False",
        "__FrozenSet__.issubset": "False",
        "__Set__.issuperset": "False",
        "__FrozenSet__.issuperset": "False",
        "__Dict__.items": "__DictItems__()",
        "__Bytes__.join": "b''",
        "__Unicode__.join": "''",
        "__Dict__.keys": "__DictKeys__()",
        "len": "0",
        "locals": "__Dict__()",
        "lower": "__T__()",
        "ljust": "__T__()",
        "lstrip": "__T__()",
        "__Bytes__.maketrans": "b''",
        "__Unicode__.maketrans": "{}",
        "__Type__.mro": "[__Type__()]",
        "oct": "''",
        "partition": "(__T__(), __T__(), __T__())",
        "__List__.pop": "self[0]",
        "__Dict__.pop": "self.keys()[0]",
        "__Set__.pop": "Any",
        "__Dict__.popitem": "self.items()[0]",
        "remove": "None",
        "replace": "__T__()",
        "repr": "''",
        "rfind": "0",
        "__List__.reverse": "None",
        "rindex": "0",
        "rjust": "__T__()",
        "round": "0.0",
        "rpartition": "(__T__(), __T__(), __T__())",
        "rsplit": "[__T__()]",
        "rstrip": "__T__()",
        "__Generator__.send": "self.__next__()",
        "__Dict__.setdefault": "self[0]",
        "__Property__.setter": "func",
        "__List__.sort": "None",
        "sorted": "__List__()",
        "split": "[__T__()]",
        "splitlines": "[self()]",
        "startswith": "False",
        "strip": "__T__()",
        "swapcase": "__T__()",
        "__Set__.symmetric_difference": "__T__()",
        "__FrozenSet__.symmetric_difference": "__T__()",
        "__Set__.symmetric_difference_update": "None",
        "__Bytes__.translate": "__T__()",
        "__Unicode__.translate": "__T__()",
        "__Generator__.throw": "None",
        "title": "__T__()",
        "to_bytes": "b''",
        "__Set__.union": "__T__()",
        "__FrozenSet__.union": "__T__()",
        "__Dict__.update": "None",
        "__Set__.update": "None",
        "upper": "__T__()",
        "__Dict__.values": "__DictValues__()",
        "zfill": "__T__()",
    })

    Signature.KNOWN_ARGSPECS.update({
        "__Type__.__call__": "(cls, *args, **kwargs)",
        "__Int__.__ceil__": "(self)",
        "__Int__.__floor__": "(self)",
        "__Float__.__getformat__": "(typestr)",
        "__Dict__.__getitem__": "(self, key)",
        "__Type__.__instancecheck__": "(self, instance)",
        "__Bool__.__init__": "(self, x)",
        "__Int__.__init__": "(self, x=0)",
        "__List__.__init__": "(self, iterable)",
        "__Tuple__.__init__": "(self, iterable)",
        "__Type__.__prepare__": "(cls, name, bases, **kwds)",
        "__Int__.__round__": "(self, ndigits=0)",
        "__Float__.__round__": "(self, ndigits=0)",
        "__List__.__reversed__": "(self)",
        "__Float__.__setformat__": "(typestr, fmt)",
        "__Dict__.__setitem__": "(self, key, value)",
        "__Set__.add": "(self, value)",
        "__List__.append": "(self, value)",
        "__Float__.as_integer_ratio": "(self)",
        "__Int__.bit_length": "(self)",
        "capitalize": "(self)",
        "casefold": "(self)",
        "__Bytes__.center": "(self, width, fillbyte=b' ')",
        "__Unicode__.center": "(self, width, fillchar=' ')",
        "clear": "(self)",
        "__Generator__.close": "(self)",
        "conjugate": "(self)",
        "copy": "(self)",
        "count": "(self, x)",
        "__Bytes__.count": "(self, sub, start=0, end=-1)",
        "__Unicode__.count": "(self, sub, start=0, end=-1)",
        "__Bytes__.decode": "(self, encoding='utf-8', errors='strict')",
        "__Property__.deleter": "(self, func)",
        "__Set__.difference": "(self, other)",
        "__FrozenSet__.difference": "(self, other)",
        "__Set__.difference_update": "(self, *others)",
        "__Set__.discard": "(self, elem)",
        "__Unicode__.encode": "(self, encoding='utf-8', errors='strict')",
        "endswith": "(self, suffix, start=0, end=-1)",
        "expandtabs": "(self, tabsize=8)",
        "__List__.extend": "(self, iterable)",
        "find": "(self, sub, start=0, end=-1)",
        "__Unicode__.format": "(self, *args, **kwargs)",
        "__Unicode__.format_map": "(self, mapping)",
        "__Bool__.from_bytes": "(bytes, byteorder, *, signed=False)",
        "__Int__.from_bytes": "(bytes, byteorder, *, signed=False)",
        "__Float__.fromhex": "(string)",
        "__Dict__.get": "(self, key, d=None)",
        "__Property__.getter": "(self, func)",
        "hex": "(self)",
        "__List__.insert": "(self, index, value)",
        "index": "(self, v)",
        "__Bytes__.index": "(self, sub, start=0, end=-1)",
        "__Unicode__.index": "(self, sub, start=0, end=-1)",
        "__Set__.intersection": "(self, other)",
        "__FrozenSet__.intersection": "(self, other)",
        "__Set__.intersection_update": "(self, *others)",
        "isalnum": "(self)",
        "isalpha": "(self)",
        "isdecimal": "(self)",
        "isdigit": "(self)",
        "isidentifier": "(self)",
        "islower": "(self)",
        "isnumeric": "(self)",
        "isprintable": "(self)",
        "isspace": "(self)",
        "istitle": "(self)",
        "isupper": "(self)",
        "__Float__.is_integer": "(self)",
        "__Set__.isdisjoint": "(self, other)",
        "__FrozenSet__.isdisjoint": "(self, other)",
        "__DictKeys__.isdisjoint": "(self, other)",
        "__DictItems__.isdisjoint": "(self, other)",
        "__Set__.issubset": "(self, other)",
        "__FrozenSet__.issubset": "(self, other)",
        "__Set__.issuperset": "(self, other)",
        "__FrozenSet__.issuperset": "(self, other)",
        "__Dict__.items": "(self)",
        "__Bytes__.join": "(self, iterable)",
        "__Unicode__.join": "(self, iterable)",
        "__Dict__.keys": "(self)",
        "lower": "(self)",
        "__Bytes__.ljust": "(self, width, fillbyte=b' ')",
        "__Unicode__.ljust": "(self, width, fillchar=' ')",
        "lstrip": "(self, chars)",
        "__Bytes__.maketrans": "(from_, to)",
        "__Unicode__.maketrans": "(x, y, z)",
        "__Type__.mro": "(cls)",
        "__Bytes__.partition": "(self, sep)",
        "__Unicode__.partition": "(self, sep)",
        "__List__.pop": "(self, index=-1)",
        "__Dict__.pop": "(self, k, d=None)",
        "__Set__.pop": "(self)",
        "__Dict__.popitem": "(self, k, d=None)",
        "__List__.remove": "(self, value)",
        "__Set__.remove": "(self, elem)",
        "replace": "(self, old, new, count=-1)",
        "__List__.reverse": "(self)",
        "rfind": "(self, sub, start=0, end=-1)",
        "rindex": "(self, sub, start=0, end=-1)",
        "__Bytes__.rjust": "(self, width, fillbyte=b' ')",
        "__Unicode__.rjust": "(self, width, fillchar=' ')",
        "__Bytes__.rpartition": "(self, sep)",
        "__Unicode__.rpartition": "(self, sep)",
        "rsplit": "(self, sep=None, maxsplit=-1)",
        "rstrip": "(self, chars=None)",
        "__Generator__.send": "(self, value)",
        "__Dict__.setdefault": "(self, k, d)",
        "__Property__.setter": "(self, func)",
        "__List__.sort": "(self)",
        "split": "(self, sep=None, maxsplit=-1)",
        "splitlines": "(self, keepends=False)",
        "strip": "(self, chars=None)",
        "startswith": "(self, prefix, start=0, end=-1)",
        "swapcase": "(self)",
        "__Set__.symmetric_difference": "(self, other)",
        "__FrozenSet__.symmetric_difference": "(self, other)",
        "__Set__.symmetric_difference_update": "(self, *others)",
        "__Generator__.throw": "(self, type, value=None, traceback=None)",
        "title": "(self)",
        "__Int__.to_bytes": "(bytes, byteorder, *, signed=False)",
        "__Bytes__.translate": "(self, table, delete=b'')",
        "__Unicode__.translate": "(self, table)",
        "__Set__.union": "(self, *others)",
        "__FrozenSet__.union": "(self, *others)",
        "__Dict__.update": "(self, d)",
        "__Set__.update": "(self, *others)",
        "upper": "(self)",
        "__Dict__.values": "(self)",
        "zfill": "(self, width)",
    })

    if sys.version[0] == '2':
        Signature.KNOWN_RESTYPES.update({
            "__BytesIterator__.__next__": None,
            "__BytesIterator__.next": "b''",
            "__UnicodeIterator__.__next__": None,
            "__UnicodeIterator__.next": "u''",
            "__Generator__.send": "self.next()",
            "__Function__.func_closure": "()",
            "__Function__.func_doc": "b''",
            "__Function__.func_name": "b''",
            "input": None,
            "raw_input": "b''",
        })

        Signature.KNOWN_ARGSPECS.update({
            "__BytesIterator__.next": "(self)",
            "__UnicodeIterator__.next": "(self)",
        })

    already_added = set()

    def add_simple(name, doc, *members):
        mi = MemberInfo(name, MemberInfo.NO_VALUE)
        mi.documentation = doc
        mi.need_imports = (state.module_name,)
        mi.members.extend(members)
        state.members.append(mi)

    def add_literal(name, literal):
        state.members.append(MemberInfo(name, None, literal=literal))

    def add_type(alias, type_obj):
        if type_obj.__name__ in already_added:
            add_literal(alias, type_obj.__name__)
            return
        already_added.add(type_obj.__name__)
        mi = MemberInfo(type_obj.__name__, type_obj, module=builtins.__name__, alias=alias)
        state.members.append(mi)
        state.members.append(MemberInfo(alias, None, literal=mi.name))

    add_simple('__Unknown__', '<unknown>', MemberInfo("__name__", None, literal='"<unknown>"'))
    add_simple('__NoneType__', 'the type of the None object', MemberInfo.NO_VALUE)

    # NoneType and None are explicitly defined to avoid parser errors
    # because of None being a keyword.
    #add_literal('NoneType', '__NoneType__')
    #add_literal('None', '__NoneType__()')

    add_type('__Object__', object)
    add_type('__Type__', type)
    
    add_type('__Int__', int)
    if type(bool()) is int:
        add_literal('__Bool__', '__Int__')
    else:
        add_type('__Bool__', bool)

    try:
        long
    except NameError:
        add_literal('__Long__', '__Int__')
    else:
        add_type('__Long__', long)

    add_type("__Float__", float)
    add_type("__Complex__", complex)

    add_type("__Tuple__", tuple)
    add_type("__List__", list)
    add_type("__Dict__", dict)
    add_type("__Set__", set)
    add_type("__FrozenSet__", frozenset)

    if bytes is not str:
        add_type("__Bytes__", bytes)
        add_type("__BytesIterator__", type(iter(bytes())))
        add_type("__Unicode__", str)
        add_type("__UnicodeIterator__", type(iter(str())))
        add_literal("__Str__", "__Unicode__")
        add_literal("__StrIterator__", "__UnicodeIterator__")

    else:
        add_type("__Bytes__", str)
        add_type("__BytesIterator__", type(iter(str())))
        add_type("__Unicode__", unicode)
        add_type("__UnicodeIterator__", type(iter(unicode())))
        add_literal("__Str__", "__Bytes__")
        add_literal("__StrIterator__", "__BytesIterator__")

    add_type("__Module__", type(inspect))
    add_type("__Function__", type(add_simple))

    add_type("__BuiltinMethodDescriptor__", type(object.__hash__))
    add_type("__BuiltinFunction__", type(abs))
    add_type("__Generator__", type((_ for _ in [])))
    add_type("__Property__", property)
    add_type("__ClassMethod__", classmethod)
    add_type("__StaticMethod__", staticmethod)
    add_type("__Ellipsis__", type(Ellipsis))
    add_type("__TupleIterator__", type(iter(())))
    add_type("__ListIterator__", type(iter([])))
    add_type("__DictKeys__", type({}.keys()))
    add_type("__DictValues__", type({}.values()))
    add_type("__DictItems__", type({}.items()))
    add_type("__SetIterator__", type(iter(set())))
    add_type("__CallableIterator__", type(iter((lambda: None), None)))

    # Also write out the builtin module names here so that we cache them
    try:
        builtin_module_names = sys.builtin_module_names
    except AttributeError:
        pass
    else:
        add_literal('__builtin_module_names__', '"' + ','.join(builtin_module_names) + '"')


if __name__ == '__main__':
    EXCLUDED_MEMBERS = ()

    outfile = sys.stdout
    if '-u8' in sys.argv:
        sys.argv.remove('-u8')
        try:
            b_outfile = outfile.buffer
        except AttributeError:
            warnings.warn("cannot enable UTF-8 output", InspectWarning)
            pass    # on Python 2, so hopefully everything is valid ASCII...
        else:
            import io
            outfile = io.TextIOWrapper(b_outfile, encoding='utf-8', errors='replace')

    if len(sys.argv) == 1:
        state = ScrapeState(builtins.__name__, builtins)
        add_builtin_objects(state)

        EXCLUDED_MEMBERS += ('None', 'False', 'True', '__debug__')
        if sys.version_info[0] == 2:
            EXCLUDED_MEMBERS += ('print',)

    elif len(sys.argv) >= 2:
        state = ScrapeState(sys.argv[1])

        if len(sys.argv) >= 3:
            state.initial_import(sys.argv[2])
        else:
            state.initial_import()

    state.collect_top_level_members()

    state.members[:] = [m for m in state.members if m.name not in EXCLUDED_MEMBERS]

    state.collect_second_level_members()

    state.dump(outfile)
    #import io
    #state.dump(io.BytesIO())
