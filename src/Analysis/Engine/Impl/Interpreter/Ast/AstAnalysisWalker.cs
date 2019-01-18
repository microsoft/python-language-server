// Python Tools for Visual Studio
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
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstAnalysisWalker : PythonWalker {
        private readonly IPythonModule _module;
        private readonly Dictionary<string, IMember> _members;
        private readonly AnalysisLogWriter _log;
        private readonly Dictionary<string, IMember> _typingScope = new Dictionary<string, IMember>();
        private readonly AstAnalysisFunctionWalkerSet _functionWalkers = new AstAnalysisFunctionWalkerSet();
        private readonly NameLookupContext _scope;
        private readonly PythonAst _ast;
        private readonly IPythonInterpreter _interpreter;
        private readonly PathResolverSnapshot _pathResolver;

        public AstAnalysisWalker(
            IPythonInterpreter interpreter,
            PathResolverSnapshot pathResolver,
            PythonAst ast,
            IPythonModule module,
            string filePath,
            Uri documentUri,
            Dictionary<string, IMember> members,
            bool includeLocationInfo,
            bool warnAboutUndefinedValues,
            bool suppressBuiltinLookup,
            AnalysisLogWriter log = null
        ) {
            _log = log ?? (interpreter as AstPythonInterpreter)?.Log;
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _members = members ?? throw new ArgumentNullException(nameof(members));
            _scope = new NameLookupContext(
                interpreter ?? throw new ArgumentNullException(nameof(interpreter)),
                interpreter.CreateModuleContext(),
                ast ?? throw new ArgumentNullException(nameof(ast)),
                _module,
                filePath,
                documentUri,
                includeLocationInfo,
                _functionWalkers,
                log: warnAboutUndefinedValues ? _log : null
            );
            _ast = ast;
            _interpreter = interpreter;
            _pathResolver = pathResolver;
            _scope.SuppressBuiltinLookup = suppressBuiltinLookup;
            _scope.PushScope(_typingScope);
            WarnAboutUndefinedValues = warnAboutUndefinedValues;
        }

        public bool CreateBuiltinTypes { get; set; }
        public bool WarnAboutUndefinedValues { get; }

        public override bool Walk(PythonAst node) {
            if (_ast != node) {
                throw new InvalidOperationException("walking wrong AST");
            }

            CollectTopLevelDefinitions();
            _scope.PushScope(_members);

            return base.Walk(node);
        }

        public override void PostWalk(PythonAst node) {
            _scope.PopScope();
            base.PostWalk(node);
        }

        public void Complete() {
            _functionWalkers.ProcessSet();

            if (_module.Name != "typing" && _scope.FilePath.EndsWithOrdinal(".pyi", ignoreCase: true)) {
                // Do not expose members directly imported from typing
                _typingScope.Clear();
            }
        }

        internal LocationInfo GetLoc(ClassDefinition node) {
            if (!_scope.IncludeLocationInfo) {
                return null;
            }
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(_ast) ?? node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            return new LocationInfo(_scope.FilePath, _scope.DocumentUri, start.Line, start.Column, end.Line, end.Column);
        }

        private LocationInfo GetLoc(Node node) => _scope.GetLoc(node);

        private IMember Clone(IMember member) =>
            member is IPythonMultipleMembers mm ? AstPythonMultipleMembers.Create(mm.GetMembers()) :
            member;

        public override bool Walk(AssignmentStatement node) {
            var value = _scope.GetValueFromExpression(node.Right);
            if ((value == null || value.MemberType == PythonMemberType.Unknown) && WarnAboutUndefinedValues) {
                _log?.Log(TraceLevel.Warning, "UndefinedValue", node.Right.ToCodeString(_ast).Trim());
            }
            if ((value as IPythonConstant)?.Type?.TypeId == BuiltinTypeId.Ellipsis) {
                value = _scope.UnknownType;
            }

            foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                AssignFromAnnotation(expr);
                if (value != _scope.UnknownType && expr.Expression is NameExpression ne) {
                    _scope.SetInScope(ne.Name, Clone(value));
                }
            }

            foreach (var ne in node.Left.OfType<NameExpression>()) {
                _scope.SetInScope(ne.Name, Clone(value));
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
                foreach (var annType in _scope.GetTypesFromAnnotation(expr.Annotation)) {
                    _scope.SetInScope(ne.Name, new AstPythonConstant(annType, GetLoc(expr.Expression)));
                    any = true;
                }
                if (!any) {
                    _scope.SetInScope(ne.Name, _scope.UnknownType);
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

                if (importNames.Length == 1 && importNames[0] == "typing") {
                    _scope.SetInScope(memberName, new AstTypingModule(), scope: _typingScope);
                } else {var imports = _pathResolver.GetImportsFromAbsoluteName(_scope.FilePath, importNames, node.ForceAbsolute);
                    switch (imports) {
                        case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                            _scope.SetInScope(memberName, _module);
                            break;
                        case ModuleImport moduleImport:
                            _scope.SetInScope(memberName, new AstNestedPythonModule(_interpreter, moduleImport.FullName));
                            break;
                        case PossibleModuleImport possibleModuleImport:
                            _scope.SetInScope(memberName, new AstNestedPythonModule(_interpreter, possibleModuleImport.PossibleModuleFullName));
                            break;
                        default:
                            _scope.SetInScope(memberName, new AstPythonConstant(_scope.UnknownType, GetLoc(memberReference)));
                            break;
                    }
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
                    case "typing":
                        ImportMembersFromTyping(node);
                        return false;
                }
            }

            var importSearchResult = _pathResolver.FindImports(_scope.FilePath, node);
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

        private void ImportMembersFromTyping(FromImportStatement node) {
            var names = node.Names;
            var asNames = node.AsNames;
            var module = new AstTypingModule();

            if (names.Count == 1 && names[0].Name == "*") {
                foreach (var memberName in module.GetMemberNames(_scope.Context)) {
                    SetMember(module, memberName, memberName);
                }
            } else {
                for (var i = 0; i < names.Count; i++) {
                    var importName = names[i].Name;
                    var memberReference = asNames[i] ?? names[i];

                    SetMember(module, importName, memberReference.Name, GetLoc(memberReference));
                }
            } 

            void SetMember(IMemberContainer container, string name, string memberName, ILocationInfo location = null) {
                var member = container.GetMember(_scope.Context, name);
                if (member == null) {
                    if (WarnAboutUndefinedValues) {
                        _log?.Log(TraceLevel.Warning, "UndefinedImport", module.Name, name);
                    }

                    member = new AstPythonConstant(_scope.UnknownType, location != null ? new[] {location} : Array.Empty<ILocationInfo>());
                }

                (member as IPythonModule)?.Imported(_scope.Context);
                _scope.SetInScope(memberName, member, scope: _typingScope);
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

                var member = _module.GetMember(_scope.Context, importName);
                _scope.SetInScope(memberName, member);
            }
        }

        private void ImportMembersFromModule(FromImportStatement node, string fullModuleName) {
            var names = node.Names;
            var asNames = node.AsNames;
            var nestedModule = new AstNestedPythonModule(_interpreter, fullModuleName);

            if (names.Count == 1 && names[0].Name == "*") {
                HandleModuleImportStar(nestedModule);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var importName = names[i].Name;
                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;
                var location = GetLoc(memberReference);

                var member = new AstNestedPythonModuleMember(importName, nestedModule, _scope.Context, location);
                _scope.SetInScope(memberName, member);
            }
        }

        private void HandleModuleImportStar(IPythonModule module) {
            module.Imported(_scope.Context);
            // Ensure child modules have been loaded
            module.GetChildrenModules();
            foreach (var memberName in module.GetMemberNames(_scope.Context)) {
                var member = module.GetMember(_scope.Context, memberName);
                if (WarnAboutUndefinedValues) {
                    if (member == null) {
                        _log?.Log(TraceLevel.Warning, "UndefinedImport", module.Name, memberName);
                    } else if (member.MemberType == PythonMemberType.Unknown) {
                        _log?.Log(TraceLevel.Warning, "UnknownImport", module.Name, memberName);
                    }
                }

                member = member ?? new AstPythonConstant(_scope.UnknownType, ((module as ILocatedMember)?.Locations).MaybeEnumerate().ToArray());
                _scope.SetInScope(memberName, member);
                (member as IPythonModule)?.Imported(_scope.Context);
            }
        }

        private void ImportMembersFromPackage(FromImportStatement node, PackageImport packageImport) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: Need tracking of previous imports to determine possible imports for namespace package. For now import nothing
                _scope.SetInScope("*", new AstPythonConstant(_scope.UnknownType, GetLoc(names[0])));
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
                    member = new AstNestedPythonModule(_interpreter, moduleImport.FullName);
                } else {
                    member = new AstPythonConstant(_scope.UnknownType, location);
                }

                _scope.SetInScope(memberName, member);
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

            var dec = (node.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault();
            foreach (var d in dec) {
                var obj = _scope.GetValueFromExpression(d);

                var declaringType = obj as IPythonType;
                var declaringModule = declaringType?.DeclaringModule;

                if (obj == _interpreter.GetBuiltinType(BuiltinTypeId.Property)) {
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
                    var existingProp = _scope.LookupNameInScopes(src.Name, NameLookupContext.LookupOptions.Local) as AstPythonProperty;
                    if (existingProp != null) {
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
            var existing = _scope.LookupNameInScopes(node.Name, NameLookupContext.LookupOptions.Local) as AstPythonProperty;
            if (existing == null) {
                existing = new AstPythonProperty(node, declaringModule, declaringType, GetLoc(node));
                _scope.SetInScope(node.Name, existing);
            }

            // Treat the rest of the property as a function. "AddOverload" takes the return type
            // and sets it as the property type.
            var funcScope = _scope.Clone();
            funcScope.SuppressBuiltinLookup = CreateBuiltinTypes;

            existing.AddOverload(CreateFunctionOverload(funcScope, node));
        }

        private IPythonFunctionOverload CreateFunctionOverload(NameLookupContext funcScope, FunctionDefinition node) {
            var parameters = node.Parameters
                .Select(p => new AstPythonParameterInfo(_ast, p, _scope.GetTypesFromAnnotation(p.Annotation)))
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
            return new AstPythonClass(node, _module,
                GetDoc(node.Body as SuiteStatement), GetLoc(node),
                CreateBuiltinTypes ? BuiltinTypeId.Unknown : BuiltinTypeId.Type); // built-ins set type later
        }

        private void CollectTopLevelDefinitions() {
            var s = (_ast.Body as SuiteStatement).Statements.ToArray();

            foreach (var node in s.OfType<FunctionDefinition>()) {
                ProcessFunctionDefinition(node);
            }

            foreach (var node in s.OfType<ClassDefinition>()) {
                _members[node.Name] = CreateClass(node);
            }

            foreach (var node in s.OfType<AssignmentStatement>().Where(n => n.Right is NameExpression)) {
                var rhs = (NameExpression)node.Right;
                if (_members.TryGetValue(rhs.Name, out var member)) {
                    foreach (var lhs in node.Left.OfType<NameExpression>()) {
                        _members[lhs.Name] = member;
                    }
                }
            }
        }

        public override bool Walk(ClassDefinition node) {
            var member = _scope.GetInScope(node.Name);
            var t = member as AstPythonClass;
            if (t == null && member is IPythonMultipleMembers mm) {
                t = mm.GetMembers().OfType<AstPythonClass>().FirstOrDefault(pt => pt.ClassDefinition.StartIndex == node.StartIndex);
            }
            if (t == null) {
                t = CreateClass(node);
                _scope.SetInScope(node.Name, t);
            }

            var bases = node.Bases.Where(a => string.IsNullOrEmpty(a.Name))
                // We cheat slightly and treat base classes as annotations.
                .SelectMany(a => _scope.GetTypesFromAnnotation(a.Expression))
                .ToArray();

            t.SetBases(_interpreter, bases);

            _scope.PushScope();
            _scope.SetInScope("__class__", t);

            return true;
        }

        public override void PostWalk(ClassDefinition node) {
            var cls = _scope.GetInScope("__class__") as AstPythonType; // Store before popping the scope
            var m = _scope.PopScope();
            if (cls != null && m != null) {
                cls.AddMembers(m, true);
            }
            base.PostWalk(node);
        }

        public void ProcessFunctionDefinition(FunctionDefinition node) {
            var existing = _scope.LookupNameInScopes(node.Name, NameLookupContext.LookupOptions.Local) as AstPythonFunction;
            if (existing == null) {
                var cls = _scope.GetInScope("__class__") as IPythonType;
                existing = new AstPythonFunction(node, _module, cls, GetLoc(node));
                _scope.SetInScope(node.Name, existing);
            }

            var funcScope = _scope.Clone();
            funcScope.SuppressBuiltinLookup = CreateBuiltinTypes;

            existing.AddOverload(CreateFunctionOverload(funcScope, node));
        }
    }
}
