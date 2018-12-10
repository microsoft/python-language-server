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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Modules;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstAnalysisWalker : PythonWalker {
        private readonly IPythonModule _module;
        private readonly PythonAst _ast;
        private readonly ExpressionLookup _lookup;
        private readonly GlobalScope _globalScope;
        private readonly AstAnalysisFunctionWalkerSet _functionWalkers = new AstAnalysisFunctionWalkerSet();

        private IPythonInterpreter Interpreter => _module.Interpreter;
        private ILogger Log => Interpreter.Log;

        public AstAnalysisWalker(IPythonModule module, PythonAst ast, bool suppressBuiltinLookup) {
            _module = module;
            _ast = ast;
            _globalScope = new GlobalScope(module);
            _lookup = new ExpressionLookup(module, ast, _globalScope, _functionWalkers) {
                SuppressBuiltinLookup = suppressBuiltinLookup
            };
            // TODO: handle typing module
        }

        public IGlobalScope GlobalScope => _globalScope;
        public bool CreateBuiltinTypes { get; set; }

        public override bool Walk(PythonAst node) {
            Check.InvalidOperation(() => _ast == node, "walking wrong AST");
            CollectTopLevelDefinitions();
            return base.Walk(node);
        }

        public IGlobalScope Complete() {
            _functionWalkers.ProcessSet();
            foreach (var childModuleName in _module.GetChildrenModuleNames()) {
                var name = $"{_module.Name}.{childModuleName}";
                _globalScope.DeclareVariable(name, new AstNestedPythonModule(name, Interpreter));
            }
            return GlobalScope;
        }

        internal LocationInfo GetLoc(ClassDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(_ast) ?? node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            return new LocationInfo(_module.FilePath, _module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        private LocationInfo GetLoc(Node node) => _lookup.GetLoc(node);

        private static IMember Clone(IMember member) =>
            member is IPythonMultipleMembers mm ? AstPythonMultipleMembers.Create(mm.GetMembers()) :
            member;

        public override bool Walk(AssignmentStatement node) {
            var value = _lookup.GetValueFromExpression(node.Right);

            if (value == null || value.MemberType == PythonMemberType.Unknown) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(_ast).Trim()}");
            }

            if ((value as IPythonConstant)?.Type?.TypeId == BuiltinTypeId.Ellipsis) {
                value = _lookup.UnknownType;
            }

            foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                AssignFromAnnotation(expr);
                if (!value.IsUnknown() && expr.Expression is NameExpression ne) {
                    _lookup.DeclareVariable(ne.Name, Clone(value));
                }
            }

            foreach (var ne in node.Left.OfType<NameExpression>()) {
                _lookup.DeclareVariable(ne.Name, Clone(value));
            }

            return base.Walk(node);
        }

        public override bool Walk(ExpressionStatement node) {
            AssignFromAnnotation(node.Expression as ExpressionWithAnnotation);
            return false;
        }

        private void AssignFromAnnotation(ExpressionWithAnnotation expr) {
            if (expr?.Annotation == null) {
                return;
            }

            if (expr.Expression is NameExpression ne) {
                var any = false;
                foreach (var annType in _lookup.GetTypesFromAnnotation(expr.Annotation)) {
                    _lookup.DeclareVariable(ne.Name, new AstPythonConstant(annType, GetLoc(expr.Expression)));
                    any = true;
                }
                if (!any) {
                    _lookup.DeclareVariable(ne.Name, _lookup.UnknownType);
                }
            }
        }

        public override bool Walk(ImportStatement node) {
            if (node.Names == null) {
                return false;
            }

            var len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (var i = 0; i < len; i++) {
                var moduleImportExpression = node.Names[i];
                var importNames = moduleImportExpression.Names.Select(n => n.Name).ToArray();
                var memberReference = node.AsNames[i] ?? moduleImportExpression.Names[0];
                var memberName = memberReference.Name;

                var imports = Interpreter.ModuleResolution.CurrentPathResolver.GetImportsFromAbsoluteName(_module.FilePath, importNames, node.ForceAbsolute);
                switch (imports) {
                    case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                        _lookup.DeclareVariable(memberName, _module);
                        break;
                    case ModuleImport moduleImport:
                        _lookup.DeclareVariable(memberName, new AstNestedPythonModule(moduleImport.FullName, Interpreter));
                        break;
                    case PossibleModuleImport possibleModuleImport:
                        _lookup.DeclareVariable(memberName, new AstNestedPythonModule(possibleModuleImport.PossibleModuleFullName, Interpreter));
                        break;
                    default:
                        _lookup.DeclareVariable(memberName, new AstPythonConstant(_lookup.UnknownType, GetLoc(memberReference)));
                        break;
                }
            }

            return false;
        }

        private static IEnumerable<KeyValuePair<string, NameExpression>> GetImportNames(IEnumerable<NameExpression> names, IEnumerable<NameExpression> asNames) {
            if (names == null) {
                return Enumerable.Empty<KeyValuePair<string, NameExpression>>();
            }
            if (asNames == null) {
                return names.Select(n => new KeyValuePair<string, NameExpression>(n.Name, n)).Where(k => !string.IsNullOrEmpty(k.Key));
            }
            return names
                .Zip(asNames.Concat(Enumerable.Repeat((NameExpression)null, int.MaxValue)),
                     (n1, n2) => new KeyValuePair<string, NameExpression>(n1?.Name, string.IsNullOrEmpty(n2?.Name) ? n1 : n2))
                .Where(k => !string.IsNullOrEmpty(k.Key));
        }

        public override bool Walk(FromImportStatement node) {
            if (node.Root == null || node.Names == null) {
                return false;
            }

            var rootNames = node.Root.Names;
            if (rootNames.Count == 1) {
                switch (rootNames[0].Name) {
                    case "__future__":
                        return false;
                    //case "typing":
                    //    ImportMembersFromTyping(node);
                    //    return false;
                }
            }

            var importSearchResult = Interpreter.ModuleResolution.CurrentPathResolver.FindImports(_module.FilePath, node);
            switch (importSearchResult) {
                case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                    ImportMembersFromSelf(node);
                    return false;
                case ModuleImport moduleImport:
                    ImportMembersFromModule(node, moduleImport.FullName);
                    return false;
                case PossibleModuleImport possibleModuleImport:
                    ImportMembersFromModule(node, possibleModuleImport.PossibleModuleFullName);
                    return false;
                case PackageImport packageImports:
                    ImportMembersFromPackage(node, packageImports);
                    return false;
                default:
                    return false;
            }
        }

        private void ImportMembersFromSelf(FromImportStatement node) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // from self import * won't define any new members
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                if (asNames[i] == null) {
                    continue;
                }

                var importName = names[i].Name;
                var memberReference = asNames[i];
                var memberName = memberReference.Name;

                var member = _module.GetMember(importName);
                _lookup.DeclareVariable(memberName, member);
            }
        }

        private void ImportMembersFromModule(FromImportStatement node, string fullModuleName) {
            var names = node.Names;
            var asNames = node.AsNames;
            var nestedModule = new AstNestedPythonModule(fullModuleName, Interpreter);

            if (names.Count == 1 && names[0].Name == "*") {
                HandleModuleImportStar(nestedModule);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var importName = names[i].Name;
                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;
                var location = GetLoc(memberReference);

                var member = new AstNestedPythonModuleMember(importName, nestedModule, location, Interpreter);
                _lookup.DeclareVariable(memberName, member);
            }
        }

        private void HandleModuleImportStar(IPythonModule module) {
            module.NotifyImported();
            // Ensure child modules have been loaded
            module.GetChildrenModuleNames();
            foreach (var memberName in module.GetMemberNames()) {
                var member = module.GetMember(memberName);
                if (member == null) {
                    Log?.Log(TraceEventType.Verbose, $"Undefined import: {module.Name}, {memberName}");
                } else if (member.MemberType == PythonMemberType.Unknown) {
                    Log?.Log(TraceEventType.Verbose, $"Unknown import: {module.Name}, {memberName}");
                }

                member = member ?? new AstPythonConstant(_lookup.UnknownType, ((module as ILocatedMember)?.Locations).MaybeEnumerate().ToArray());
                _lookup.DeclareVariable(memberName, member);
                (member as IPythonModule)?.NotifyImported();
            }
        }

        private void ImportMembersFromPackage(FromImportStatement node, PackageImport packageImport) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: Need tracking of previous imports to determine possible imports for namespace package. For now import nothing
                _lookup.DeclareVariable("*", new AstPythonConstant(_lookup.UnknownType, GetLoc(names[0])));
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var importName = names[i].Name;
                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;
                var location = GetLoc(memberReference);

                ModuleImport moduleImport;
                IMember member;
                if ((moduleImport = packageImport.Modules.FirstOrDefault(mi => mi.Name.EqualsOrdinal(importName))) != null) {
                    member = new AstNestedPythonModule(moduleImport.FullName, Interpreter);
                } else {
                    member = new AstPythonConstant(_lookup.UnknownType, location);
                }

                _lookup.DeclareVariable(memberName, member);
            }
        }

        public override bool Walk(IfStatement node) {
            var allValidComparisons = true;
            foreach (var test in node.Tests) {
                if (test.Test is BinaryExpression cmp &&
                    cmp.Left is MemberExpression me && (me.Target as NameExpression)?.Name == "sys" && me.Name == "version_info" &&
                    cmp.Right is TupleExpression te && te.Items.All(i => (i as ConstantExpression)?.Value is int)) {
                    Version v;
                    try {
                        v = new Version(
                            (int)((te.Items.ElementAtOrDefault(0) as ConstantExpression)?.Value ?? 0),
                            (int)((te.Items.ElementAtOrDefault(1) as ConstantExpression)?.Value ?? 0)
                        );
                    } catch (ArgumentException) {
                        // Unsupported comparison, so walk all children
                        return true;
                    }

                    var shouldWalk = false;
                    switch (cmp.Operator) {
                        case PythonOperator.LessThan:
                            shouldWalk = _ast.LanguageVersion.ToVersion() < v;
                            break;
                        case PythonOperator.LessThanOrEqual:
                            shouldWalk = _ast.LanguageVersion.ToVersion() <= v;
                            break;
                        case PythonOperator.GreaterThan:
                            shouldWalk = _ast.LanguageVersion.ToVersion() > v;
                            break;
                        case PythonOperator.GreaterThanOrEqual:
                            shouldWalk = _ast.LanguageVersion.ToVersion() >= v;
                            break;
                    }
                    if (shouldWalk) {
                        // Supported comparison, so only walk the one block
                        test.Walk(this);
                        return false;
                    }
                } else {
                    allValidComparisons = false;
                }
            }
            return !allValidComparisons;
        }

        public override bool Walk(FunctionDefinition node) {
            if (node.IsLambda) {
                return false;
            }

            var dec = (node.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault().ToArray();
            foreach (var d in dec) {
                var obj = _lookup.GetValueFromExpression(d);

                var declaringType = obj as IPythonType;
                var declaringModule = declaringType?.DeclaringModule;

                if (declaringType?.TypeId == BuiltinTypeId.Property) {
                    AddProperty(node, declaringModule, declaringType);
                    return false;
                }

                var name = declaringType?.Name;
                if (declaringModule?.Name == "abc" && name == "abstractproperty") {
                    AddProperty(node, declaringModule, declaringType);
                    return false;
                }
            }
            foreach (var setter in dec.OfType<MemberExpression>().Where(n => n.Name == "setter")) {
                if (setter.Target is NameExpression src) {
                    if (_lookup.LookupNameInScopes(src.Name, ExpressionLookup.LookupOptions.Local) is AstPythonProperty existingProp) {
                        // Setter for an existing property, so don't create a function
                        existingProp.MakeSettable();
                        return false;
                    }
                }
            }

            ProcessFunctionDefinition(node);
            // Do not recurse into functions
            return false;
        }

        private void AddProperty(FunctionDefinition node, IPythonModule declaringModule, IPythonType declaringType) {
            if (!(_lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is AstPythonProperty existing)) {
                existing = new AstPythonProperty(node, declaringModule, declaringType, GetLoc(node));
                _lookup.DeclareVariable(node.Name, existing);
            }

            if (!_functionWalkers.Contains(node)) {
                // Treat the rest of the property as a function. "AddOverload" takes the return type
                // and sets it as the property type.
                var funcScope = _lookup.Clone();
                funcScope.SuppressBuiltinLookup = CreateBuiltinTypes;
                existing.AddOverload(CreateFunctionOverload(funcScope, node));
            }
        }

        private IPythonFunctionOverload CreateFunctionOverload(ExpressionLookup funcScope, FunctionDefinition node) {
            var parameters = node.Parameters
                .Select(p => new AstPythonParameterInfo(_ast, p, _lookup.GetTypesFromAnnotation(p.Annotation)))
                .ToArray();

            var overload = new AstPythonFunctionOverload(
                parameters,
                funcScope.GetLocOfName(node, node.NameExpression),
                node.ReturnAnnotation?.ToCodeString(_ast));
            _functionWalkers.Add(new AstAnalysisFunctionWalker(funcScope, node, overload));

            return overload;
        }

        private static string GetDoc(SuiteStatement node) {
            var docExpr = node?.Statements?.FirstOrDefault() as ExpressionStatement;
            var ce = docExpr?.Expression as ConstantExpression;
            return ce?.Value as string;
        }

        private AstPythonClass CreateClass(ClassDefinition node) {
            node = node ?? throw new ArgumentNullException(nameof(node));
            return new AstPythonClass(
                node,
                _module,
                GetDoc(node.Body as SuiteStatement),
                GetLoc(node),
                Interpreter,
                CreateBuiltinTypes ? BuiltinTypeId.Unknown : BuiltinTypeId.Type); // built-ins set type later
        }

        private void CollectTopLevelDefinitions() {
            var s = (_ast.Body as SuiteStatement)?.Statements.ToArray() ?? Array.Empty<Statement>();

            foreach (var node in s.OfType<FunctionDefinition>()) {
                ProcessFunctionDefinition(node);
            }

            foreach (var node in s.OfType<ClassDefinition>()) {
                _lookup.DeclareVariable(node.Name, CreateClass(node));
            }

            foreach (var node in s.OfType<AssignmentStatement>().Where(n => n.Right is NameExpression)) {
                var rhs = (NameExpression)node.Right;
                if (_lookup.CurrentScope.Variables.TryGetValue(rhs.Name, out var member)) {
                    foreach (var lhs in node.Left.OfType<NameExpression>()) {
                        _lookup.DeclareVariable(lhs.Name, member);
                    }
                }
            }
        }

        public override bool Walk(ClassDefinition node) {
            var member = _lookup.GetInScope(node.Name);
            var t = member as AstPythonClass;
            if (t == null && member is IPythonMultipleMembers mm) {
                t = mm.GetMembers().OfType<AstPythonClass>().FirstOrDefault(pt => pt.ClassDefinition.StartIndex == node.StartIndex);
            }
            if (t == null) {
                t = CreateClass(node);
                _lookup.DeclareVariable(node.Name, t);
            }

            var bases = node.Bases.Where(a => string.IsNullOrEmpty(a.Name))
                // We cheat slightly and treat base classes as annotations.
                .SelectMany(a => _lookup.GetTypesFromAnnotation(a.Expression))
                .ToArray();

            t.SetBases(Interpreter, bases);

            _lookup.OpenScope(node);
            _lookup.DeclareVariable("__class__", t);

            return true;
        }

        public override void PostWalk(ClassDefinition node) {
            if (_lookup.GetInScope("__class__") is AstPythonType cls) {
                var m = _lookup.CloseScope();
                if (m != null) {
                    cls.AddMembers(m.Variables, true);
                }
            }
            base.PostWalk(node);
        }

        public void ProcessFunctionDefinition(FunctionDefinition node) {
            if (!(_lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is AstPythonFunction existing)) {
                var cls = _lookup.GetInScope("__class__") as IPythonType;
                existing = new AstPythonFunction(node, _module, cls, GetLoc(node));
                _lookup.DeclareVariable(node.Name, existing);
            }

            if (!_functionWalkers.Contains(node)) {
                var funcScope = _lookup.Clone();
                funcScope.SuppressBuiltinLookup = CreateBuiltinTypes;
                existing.AddOverload(CreateFunctionOverload(funcScope, node));
            }
        }
    }
}
