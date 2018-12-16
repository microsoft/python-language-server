﻿// Copyright(c) Microsoft Corporation
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Modules;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AnalysisWalker : PythonWalkerAsync {
        private readonly IServiceContainer _services;
        private readonly IPythonInterpreter _interpreter;
        private readonly ILogger _log;
        private readonly IPythonModule _module;
        private readonly PythonAst _ast;
        private readonly ExpressionLookup _lookup;
        private readonly GlobalScope _globalScope;
        private readonly AnalysisFunctionWalkerSet _functionWalkers = new AnalysisFunctionWalkerSet();
        private readonly bool _suppressBuiltinLookup;
        private IDisposable _classScope;

        public AnalysisWalker(IServiceContainer services, IPythonModule module, PythonAst ast, bool suppressBuiltinLookup) {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _ast = ast ?? throw new ArgumentNullException(nameof(ast));

            _interpreter = services.GetService<IPythonInterpreter>();
            _log = services.GetService<ILogger>();
            _globalScope = new GlobalScope(module);
            _lookup = new ExpressionLookup(_services, module, ast, _globalScope, _functionWalkers);
            _suppressBuiltinLookup = suppressBuiltinLookup;
            // TODO: handle typing module
        }

        public IGlobalScope GlobalScope => _globalScope;

        public override Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) {
            Check.InvalidOperation(() => _ast == node, "walking wrong AST");
            CollectTopLevelDefinitions();

            cancellationToken.ThrowIfCancellationRequested();
            return base.WalkAsync(node, cancellationToken);
        }

        public async Task<IGlobalScope> CompleteAsync(CancellationToken cancellationToken = default) {
            await _functionWalkers.ProcessSetAsync(cancellationToken);
            foreach (var childModuleName in _module.GetChildrenModuleNames()) {
                var name = $"{_module.Name}.{childModuleName}";
                _globalScope.DeclareVariable(name, _module);
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

        private static IPythonType Clone(IPythonType type) =>
            type is IPythonMultipleTypes mm ? PythonMultipleTypes.Create(mm.Types) :
            type;

        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await _lookup.GetValueFromExpressionAsync(node.Right, cancellationToken);

            if (value == null || value.MemberType == PythonMemberType.Unknown) {
                _log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(_ast).Trim()}");
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

            return await base.WalkAsync(node, cancellationToken);
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

        public override async Task<bool> WalkAsync(ImportStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Names == null) {
                return false;
            }

            var len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (var i = 0; i < len; i++) {
                var moduleImportExpression = node.Names[i];
                var importNames = moduleImportExpression.Names.Select(n => n.Name).ToArray();
                var memberReference = node.AsNames[i] ?? moduleImportExpression.Names[0];
                var memberName = memberReference.Name;

                var imports = _interpreter.ModuleResolution.CurrentPathResolver.GetImportsFromAbsoluteName(_module.FilePath, importNames, node.ForceAbsolute);
                switch (imports) {
                    case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                        _lookup.DeclareVariable(memberName, _module);
                        break;
                    case ModuleImport moduleImport:
                        var m1 = await _interpreter.ModuleResolution.ImportModuleAsync(moduleImport.FullName, cancellationToken);
                        _lookup.DeclareVariable(memberName, m1 ?? new SentinelModule(moduleImport.FullName));
                        break;
                    case PossibleModuleImport possibleModuleImport:
                        var m2 = await _interpreter.ModuleResolution.ImportModuleAsync(possibleModuleImport.PossibleModuleFullName, cancellationToken);
                        _lookup.DeclareVariable(memberName, m2 ?? new SentinelModule(possibleModuleImport.PossibleModuleFullName));
                        break;
                    default:
                        _lookup.DeclareVariable(memberName, new SentinelModule(memberName));
                        break;
                }
            }

            return false;
        }

        public override async Task<bool> WalkAsync(FromImportStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
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

            var importSearchResult = _interpreter.ModuleResolution.CurrentPathResolver.FindImports(_module.FilePath, node);
            switch (importSearchResult) {
                case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                    ImportMembersFromSelf(node);
                    return false;
                case ModuleImport moduleImport:
                    await ImportMembersFromModuleAsync(node, moduleImport.FullName, cancellationToken);
                    return false;
                case PossibleModuleImport possibleModuleImport:
                    await ImportMembersFromModuleAsync(node, possibleModuleImport.PossibleModuleFullName, cancellationToken);
                    return false;
                case PackageImport packageImports:
                    await ImportMembersFromPackageAsync(node, packageImports, cancellationToken);
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

        private async Task ImportMembersFromModuleAsync(FromImportStatement node, string moduleName, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;
            var module = await _interpreter.ModuleResolution.ImportModuleAsync(moduleName, cancellationToken);

            if (names.Count == 1 && names[0].Name == "*") {
                await HandleModuleImportStarAsync(module, cancellationToken);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var importName = names[i].Name;
                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;

                var type = module.GetMember(memberReference.Name) ?? _lookup.UnknownType;
                _lookup.DeclareVariable(memberName, type);
            }
        }

        private async Task HandleModuleImportStarAsync(IPythonModule module, CancellationToken cancellationToken = default) {
            // Ensure child modules have been loaded
            module.GetChildrenModuleNames();

            foreach (var memberName in module.GetMemberNames()) {
                cancellationToken.ThrowIfCancellationRequested();

                var member = module.GetMember(memberName);
                if (member == null) {
                    _log?.Log(TraceEventType.Verbose, $"Undefined import: {module.Name}, {memberName}");
                } else if (member.MemberType == PythonMemberType.Unknown) {
                    _log?.Log(TraceEventType.Verbose, $"Unknown import: {module.Name}, {memberName}");
                }

                member = member ?? new AstPythonConstant(_lookup.UnknownType, module.Locations.MaybeEnumerate().ToArray());
                if (member is IPythonModule m) {
                    await _interpreter.ModuleResolution.ImportModuleAsync(m.Name, cancellationToken);
                }
                _lookup.DeclareVariable(memberName, member);
            }
        }

        private async Task ImportMembersFromPackageAsync(FromImportStatement node, PackageImport packageImport, CancellationToken cancellationToken = default) {
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
                IPythonType member;
                if ((moduleImport = packageImport.Modules.FirstOrDefault(mi => mi.Name.EqualsOrdinal(importName))) != null) {
                    member = await _interpreter.ModuleResolution.ImportModuleAsync(moduleImport.FullName, cancellationToken);
                } else {
                    member = new AstPythonConstant(_lookup.UnknownType, location);
                }

                _lookup.DeclareVariable(memberName, member);
            }
        }

        public override async Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default) {
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
                        await test.WalkAsync(this, cancellationToken);
                        return false;
                    }
                } else {
                    allValidComparisons = false;
                }
            }
            return !allValidComparisons;
        }

        public override async Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            if (node.IsLambda) {
                return false;
            }

            var dec = (node.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault().ToArray();
            foreach (var d in dec) {
                var declaringType = await _lookup.GetValueFromExpressionAsync(d, cancellationToken);
                if (declaringType != null) {
                    var declaringModule = declaringType.DeclaringModule;

                    if (declaringType.TypeId == BuiltinTypeId.Property) {
                        AddProperty(node, declaringModule, declaringType);
                        return false;
                    }

                    var name = declaringType.Name;
                    if (declaringModule?.Name == "abc" && name == "abstractproperty") {
                        AddProperty(node, declaringModule, declaringType);
                        return false;
                    }
                }
            }

            foreach (var setter in dec.OfType<MemberExpression>().Where(n => n.Name == "setter")) {
                if (setter.Target is NameExpression src) {
                    if (_lookup.LookupNameInScopes(src.Name, ExpressionLookup.LookupOptions.Local) is PythonProperty existingProp) {
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
            if (!(_lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is PythonProperty existing)) {
                existing = new PythonProperty(node, declaringModule, declaringType, GetLoc(node));
                _lookup.DeclareVariable(node.Name, existing);
            }

            if (!_functionWalkers.Contains(node)) {
                // Treat the rest of the property as a function. "AddOverload"
                // takes the return type and sets it as the property type.
                existing.AddOverload(CreateFunctionOverload(_lookup, node));
            }
        }

        private IPythonFunctionOverload CreateFunctionOverload(ExpressionLookup lookup, FunctionDefinition node) {
            var parameters = node.Parameters
                .Select(p => new ParameterInfo(_ast, p, _lookup.GetTypesFromAnnotation(p.Annotation)))
                .ToArray();

            var overload = new PythonFunctionOverload(
                parameters,
                lookup.GetLocOfName(node, node.NameExpression),
                node.ReturnAnnotation?.ToCodeString(_ast));

            _functionWalkers.Add(new AnalysisFunctionWalker(lookup, node, overload));
            return overload;
        }

        private static string GetDoc(SuiteStatement node) {
            var docExpr = node?.Statements?.FirstOrDefault() as ExpressionStatement;
            var ce = docExpr?.Expression as ConstantExpression;
            return ce?.Value as string;
        }

        private PythonClass CreateClass(ClassDefinition node) {
            node = node ?? throw new ArgumentNullException(nameof(node));
            return new PythonClass(
                node,
                _module,
                GetDoc(node.Body as SuiteStatement),
                GetLoc(node),
                _interpreter,
                _suppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type); // built-ins set type later
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
                var t = _lookup.CurrentScope.Variables.GetMember(rhs.Name);
                if (t != null) {
                    foreach (var lhs in node.Left.OfType<NameExpression>()) {
                        _lookup.DeclareVariable(lhs.Name, t);
                    }
                }
            }
        }

        public override Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            var member = _lookup.GetInScope(node.Name);
            var t = member as PythonClass;
            if (t == null && member is IPythonMultipleTypes mm) {
                t = mm.Types.OfType<PythonClass>().FirstOrDefault(pt => pt.ClassDefinition.StartIndex == node.StartIndex);
            }
            if (t == null) {
                t = CreateClass(node);
                _lookup.DeclareVariable(node.Name, t);
            }

            var bases = node.Bases.Where(a => string.IsNullOrEmpty(a.Name))
                // We cheat slightly and treat base classes as annotations.
                .SelectMany(a => _lookup.GetTypesFromAnnotation(a.Expression))
                .ToArray();

            t.SetBases(_interpreter, bases);

            _classScope = _lookup.CreateScope(node, _lookup.CurrentScope);
            _lookup.DeclareVariable("__class__", t);

            return Task.FromResult(true);
        }

        public override Task PostWalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            if (_lookup.GetInScope("__class__") is PythonType cls) {
                cls.AddMembers(_lookup.CurrentScope.Variables, true);
                _classScope?.Dispose();
            }
            return base.PostWalkAsync(node, cancellationToken);
        }

        public void ProcessFunctionDefinition(FunctionDefinition node) {
            if (!(_lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is PythonFunction existing)) {
                var cls = _lookup.GetInScope("__class__") as IPythonType;
                existing = new PythonFunction(node, _module, cls, GetLoc(node));
                _lookup.DeclareVariable(node.Name, existing);
            }

            if (!_functionWalkers.Contains(node)) {
                existing.AddOverload(CreateFunctionOverload(_lookup, node));
            }
        }
    }
}
