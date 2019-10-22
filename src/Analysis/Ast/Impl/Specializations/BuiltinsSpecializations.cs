// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Specializations {
    public static class BuiltinsSpecializations {
        public static IMember Identity(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var args = argSet.Values<IMember>();
            return args.Count > 0 ? args.FirstOrDefault(a => !a.IsUnknown()) ?? args[0] : null;
        }

        public static IMember TypeInfo(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var args = argSet.Values<IMember>();
            var t = args.Count > 0 ? args[0].GetPythonType() : module.Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            return t.ToBound();
        }

        public static IMember Iterator(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var args = argSet.Values<IMember>();
            if (args.Count > 0) {
                if (args[0] is IPythonCollection seq) {
                    return seq.GetIterator();
                }
                var t = args[0].GetPythonType();
                if (t.IsBuiltin && t.Name == "str") {
                    return new PythonTypeIterator(BuiltinTypeId.StrIterator, BuiltinTypeId.Str, module.Interpreter);
                }
            }
            return null;
        }

        public static IMember List(IPythonInterpreter interpreter, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan)
            => PythonCollectionType.CreateList(interpreter.ModuleResolution.BuiltinsModule, argSet);

        public static IMember ListOfStrings(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var type = new TypingListType("List", module.Interpreter.GetBuiltinType(BuiltinTypeId.Str), module.Interpreter, false);
            return new TypingList(type);
        }
        public static IMember DictStringToObject(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var str = module.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var obj = module.Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            var type = new TypingDictionaryType("Dict", str, obj, module.Interpreter, false);
            return new TypingDictionary(type);
        }

        public static IMember Next(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var args = argSet.Values<IMember>();
            return args.Count > 0 && args[0] is IPythonIterator it ? it.Next : null;
        }

        public static IMember __iter__(IPythonInterpreter interpreter, BuiltinTypeId contentTypeId) {
            var location = new Location(interpreter.ModuleResolution.BuiltinsModule);
            var fn = new PythonFunctionType(@"__iter__", location, null, string.Empty);
            var o = new PythonFunctionOverload(fn, location);
            o.AddReturnValue(PythonTypeIterator.FromTypeId(interpreter, contentTypeId));
            fn.AddOverload(o);
            return fn;
        }

        public static IMember Range(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var args = argSet.Values<IMember>();
            if (args.Count > 0) {
                var type = new PythonCollectionType(BuiltinTypeId.List, module.Interpreter.ModuleResolution.BuiltinsModule, false);
                return new PythonCollection(type, new[] { args[0] });
            }
            return null;
        }

        public static IMember CollectionItem(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var args = argSet.Values<IMember>();
            return args.Count > 0 && args[0] is PythonCollection c ? c.Contents.FirstOrDefault() : null;
        }

        public static IMember Super(IPythonModule declaringModule, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            if (argSet.Arguments.Any()) {
                //multi argument form can have 1 or 2 arguments
                var classType = argSet.Argument<IMember>(0).GetPythonType<IPythonClassType>();
                var objOrType = argSet.Argument<IMember>(1).GetPythonType<IPythonClassType>();

                bool isInstance = objOrType.Equals(classType);
                bool isSubClass = objOrType.Bases.Any(b => classType.Equals(b));
                if (!isInstance &&
                    (!isSubClass ||
                    classType?.Mro == null)) {
                    return null;
                }

                // Trim mro 
                var mro = classType.Mro.ToList();
                var start = mro.FindIndex(0, i => i.Name == objOrType.Name);
                var remainingMro = mro.GetRange(start, mro.Count() - start);

                var nextClassInLine = remainingMro.FirstOrDefault();
                if (nextClassInLine != null) {
                    var location = new Location(nextClassInLine.Location.Module, nextClassInLine.Location.IndexSpan);
                    var proxySuper = new PythonSuperType(location, remainingMro);
                    return proxySuper.CreateInstance(argSet);
                }
                return null;
            } else {
                //Zero argument form only works inside a class definition
                foreach (var s in argSet.Eval.CurrentScope.EnumerateTowardsGlobal) {
                    if (s.Node is ClassDefinition) {
                        var classType = s.Variables["__class__"].GetPythonType<IPythonClassType>();

                        if (classType?.Mro != null) {
                            var nextClassInLine = classType.Mro?.Skip(1).FirstOrDefault();
                            var location = new Location(nextClassInLine.Location.Module, nextClassInLine.Location.IndexSpan);
                            var proxySuper = new PythonSuperType(location, classType.Mro);
                            return proxySuper.CreateInstance(argSet);
                        }
                    }
                }
            }
            return null;
        }

        public static IMember Open(IPythonModule declaringModule, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            var mode = argSet.GetArgumentValue<IPythonConstant>("mode");

            var binary = false;
            var writable = false;
            var readWrite = false;

            var modeString = mode?.GetString();
            if (modeString != null) {
                binary = modeString.Contains("b");
                writable = modeString.Contains("w") || modeString.Contains("a") || modeString.Contains("x");
                readWrite = writable && modeString.Contains("r");
            }

            string returnTypeName;
            var io = declaringModule.Interpreter.ModuleResolution.GetImportedModule("io");
            if (binary) {
                returnTypeName = writable ?
                    readWrite ? "BufferedRandom" : "BufferedWriter"
                    : "BufferedReader";
            } else {
                returnTypeName = "TextIOWrapper";
            }

            var returnType = io?.GetMember(returnTypeName)?.GetPythonType();
            return returnType != null ? returnType.CreateInstance(argSet) : null;
        }

        public static IMember GetAttr(IPythonModule module, IPythonFunctionOverload overload, IArgumentSet argSet, IndexSpan indexSpan) {
            // TODO: Try __getattr__ first; this may not be as reliable in practice
            // given we could be assuming that __getattr__ always returns the same type,
            // which is incorrect more often than not.

            var args = argSet.Values<IMember>();
            if (args.Count < 2) {
                return null;
            }

            var o = args[0];
            var name = (args[1] as IPythonConstant)?.GetString();

            IMember def = null;
            if (args.Count >= 3) {
                def = args[2];
            }

            // second argument to getattr was not a string, which is a runtime error
            // getattr(a, 3.14)
            if (name == null) {
                // TODO diagnostic error when second arg of getattr is not a string
                return module.Interpreter.UnknownType;
            }

            return o?.GetPythonType().GetMember(name) ?? def;
        }
    }
}
