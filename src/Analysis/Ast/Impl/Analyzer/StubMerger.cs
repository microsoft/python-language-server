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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class StubMerger {
        private readonly ExpressionEval _eval;

        public StubMerger(ExpressionEval eval) {
            _eval = eval;
        }

        /// <summary>
        /// Merges data from stub with the data from the module.
        /// </summary>
        /// <remarks>
        /// Types are taken from the stub while location and documentation comes from 
        /// source so code navigation takes user to the source and not to the stub. 
        /// Stub data, such as class methods are augmented by methods from source
        /// since stub is not guaranteed to be complete.
        /// </remarks>
        public void MergeStub(IDocumentAnalysis stubAnalysis, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (_eval.Module.ModuleType == ModuleType.User || _eval.Module.ModuleType == ModuleType.Stub) {
                return;
            }
            // No stub, no merge.
            if (stubAnalysis.IsEmpty()) {
                return;
            }
            // https://github.com/microsoft/python-language-server/issues/907
            Debug.Assert(!(stubAnalysis is EmptyAnalysis));

            // Stub is the primary information source. Take types from the stub
            // and replace source types by stub types. Transfer location and documentation
            // from source members to the stub types.
            TransferTypesFromStub(stubAnalysis, cancellationToken);

            UpdateVariables();
        }

        /// <summary>
        /// Transfers types from stub to source while preserving documentation and location.
        /// </summary>
        /// <remarks>
        /// Stub is the primary information source. Take types from the stub
        /// and replace source types by stub types. Transfer location and documentation
        /// from source members to the stub types.
        /// </remarks> 
        private void TransferTypesFromStub(IDocumentAnalysis stubAnalysis, CancellationToken cancellationToken) {
            foreach (var v in stubAnalysis.GlobalScope.Variables) {
                cancellationToken.ThrowIfCancellationRequested();

                var stubType = v.Value.GetPythonType();
                if (stubType.IsUnknown()) {
                    continue;
                }
                // If stub says 'Any' but we have better type, keep the current type.
                if (stubType.DeclaringModule is TypingModule && stubType.Name == "Any") {
                    continue;
                }

                var sourceVar = _eval.GlobalScope.Variables[v.Name];
                var sourceType = sourceVar?.Value.GetPythonType();

                if (sourceVar?.Source == VariableSource.Import &&
                   sourceVar.GetPythonType()?.DeclaringModule.Stub != null) {
                    // Keep imported types as they are defined in the library. For example,
                    // 'requests' imports NullHandler as 'from logging import NullHandler'.
                    // But 'requests' also declares NullHandler in its stub (but not in the main code)
                    // and that declaration does not have documentation or location. Therefore avoid
                    // taking types that are stub-only when similar type is imported from another
                    // module that also has a stub.
                    continue;
                }

                TryReplaceMember(v, sourceType, stubType, cancellationToken);
            }
        }

        private void TryReplaceMember(IVariable v, IPythonType sourceType, IPythonType stubType, CancellationToken cancellationToken) {
            // If type does not exist in module, but exists in stub, declare it unless it is an import.
            // If types are the classes, take class from the stub, then add missing members.
            // Otherwise, replace type by one from the stub.
            switch (sourceType) {
                case null:
                    // Nothing in source, but there is type in the stub. Declare it.
                    if (v.Source == VariableSource.Declaration || v.Source == VariableSource.Generic) {
                        _eval.DeclareVariable(v.Name, v.Value, v.Source);
                    }
                    break;

                case PythonClassType sourceClass:
                    MergeClass(v, sourceClass, stubType, cancellationToken);
                    break;

                case IPythonModule _:
                    // We do not re-declare modules.
                    break;

                default:
                    var stubModule = stubType.DeclaringModule;
                    if (stubType is IPythonModule || stubModule.ModuleType == ModuleType.Builtins) {
                        // Modules members that are modules should remain as they are, i.e. os.path
                        // should remain library with its own stub attached.
                        break;
                    }
                    // We do not re-declaring variables that are imported.
                    if (v.Source == VariableSource.Declaration) {
                        TransferDocumentationAndLocation(sourceType, stubType);
                        // Re-declare variable with the data from the stub.
                        var source = _eval.CurrentScope.Variables[v.Name]?.Source ?? v.Source;
                        _eval.DeclareVariable(v.Name, v.Value, source);
                    }

                    break;
            }
        }

        private void MergeClass(IVariable v, IPythonClassType sourceClass, IPythonType stubType, CancellationToken cancellationToken) {
            // Transfer documentation first so we get class documentation
            // that comes from the class definition win over one that may
            // come from __init__ during the member merge below.
            TransferDocumentationAndLocation(sourceClass, stubType);

            // Replace the class entirely since stub members may use generic types
            // and the class definition is important. We transfer missing members
            // from the original class to the stub.
            _eval.DeclareVariable(v.Name, v.Value, v.Source);

            // First pass: go through source class members and pick those 
            // that are not present in the stub class.
            foreach (var name in sourceClass.GetMemberNames()) {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceMember = sourceClass.GetMember(name);
                if (sourceMember.IsUnknown()) {
                    continue; // Do not add unknowns to the stub.
                }
                var sourceMemberType = sourceMember?.GetPythonType();
                if (sourceMemberType is IPythonClassMember cm && cm.DeclaringType != sourceClass) {
                    continue; // Only take members from this class and not from bases.
                }
                if (!IsAcceptableModule(sourceMemberType)) {
                    continue; // Member does not come from module or its submodules.
                }

                var stubMember = stubType.GetMember(name);
                var stubMemberType = stubMember.GetPythonType();

                // Get documentation from the current type, if any, since stubs
                // typically do not contain documentation while scraped code does.
                TransferDocumentationAndLocation(sourceMemberType, stubMemberType);

                // If stub says 'Any' but we have better type, use member from the original class.
                if (stubMember != null && !(stubType.DeclaringModule is TypingModule && stubType.Name == "Any")) {
                    continue; // Stub already have the member, don't replace.
                }

                (stubType as PythonType)?.AddMember(name, stubMember, overwrite: true);
            }

            // Second pass: go through stub class members and if they don't have documentation 
            // or location, check if source class has same member and fetch it from there. 
            // The reason is that in the stub sometimes members are specified all in one 
            // class while in source they may come from bases. Example: datetime.
            foreach (var name in stubType.GetMemberNames()) {
                cancellationToken.ThrowIfCancellationRequested();

                var stubMember = stubType.GetMember(name);
                if (stubMember.IsUnknown()) {
                    continue;
                }
                var stubMemberType = stubMember.GetPythonType();
                if (stubMemberType is IPythonClassMember cm && cm.DeclaringType != stubType) {
                    continue; // Only take members from this class and not from bases.
                }

                var sourceMember = sourceClass.GetMember(name);
                if (sourceMember.IsUnknown()) {
                    continue;
                }

                var sourceMemberType = sourceMember.GetPythonType();
                if (sourceMemberType.Location.IsValid && !stubMemberType.Location.IsValid) {
                    TransferDocumentationAndLocation(sourceMemberType, stubMemberType);
                }
            }
        }

        private void UpdateVariables() {
            // Second pass: if we replaced any classes by new from the stub, we need to update 
            // variables that may still be holding old content. For example, ctypes
            // declares 'c_voidp = c_void_p' so when we replace 'class c_void_p'
            // by class from the stub, we need to go and update 'c_voidp' variable.
            foreach (var v in _eval.GlobalScope.Variables.Where(v => v.Source == VariableSource.Declaration)) {
                var variableType = v.Value.GetPythonType();
                if (!IsAcceptableModule(variableType)) {
                    continue;
                }
                // Check if type that the variable references actually declared here.
                var typeInScope = _eval.GlobalScope.Variables[variableType.Name].GetPythonType();
                if (typeInScope == null || variableType == typeInScope) {
                    continue;
                }

                if (v.Value == variableType) {
                    _eval.DeclareVariable(v.Name, typeInScope, v.Source);
                } else if (v.Value is IPythonInstance) {
                    _eval.DeclareVariable(v.Name, new PythonInstance(typeInScope), v.Source);
                }
            }
        }

        private void TransferDocumentationAndLocation(IPythonType sourceType, IPythonType stubType) {
            if (sourceType.IsUnknown() || sourceType.DeclaringModule.ModuleType == ModuleType.Builtins ||
                stubType.IsUnknown() || stubType.DeclaringModule.ModuleType == ModuleType.Builtins) {
                return; // Do not transfer location of unknowns or builtins
            }

            // Stub may be one for multiple modules - when module consists of several
            // submodules, there is typically only one stub for the main module.
            // Types from 'unittest.case' (library) are stubbed in 'unittest' stub.
            if (!IsAcceptableModule(sourceType)) {
                return; // Do not change unrelated types.
            }

            // Destination must be from this module stub and not from other modules.
            // Consider that 'email.headregistry' stub has DataHeader declaring 'datetime'
            // property of type 'datetime' from 'datetime' module. We don't want to modify
            // datetime type and change it's location to 'email.headregistry'.
            if(stubType.DeclaringModule.ModuleType != ModuleType.Stub || stubType.DeclaringModule != _eval.Module.Stub) {
                return;
            }

            // Documentation and location are always get transferred from module type
            // to the stub type and never the other way around. This makes sure that
            // we show documentation from the original module and goto definition
            // navigates to the module source and not to the stub.
            if (sourceType != stubType && sourceType is PythonType src && stubType is PythonType dst) {
                // If type is a class, then doc can either come from class definition node of from __init__.
                // If class has doc from the class definition, don't stomp on it.
                if (src is PythonClassType srcClass && dst is PythonClassType dstClass) {
                    // Higher lever source wins
                    if (srcClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Class ||
                       (srcClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Init && dstClass.DocumentationSource == PythonClassType.ClassDocumentationSource.Base)) {
                        dstClass.SetDocumentation(srcClass.Documentation);
                    }
                } else {
                    // Sometimes destination (stub type) already has documentation. This happens when stub type 
                    // is used to augment more than one type. For example, in threading module RLock stub class
                    // replaces both  RLock function and _RLock class making 'factory' function RLock to look 
                    // like a class constructor. Effectively a single  stub type is used for more than one type
                    // in the source and two source types may have different documentation. Thus transferring doc
                    // from one source type affects documentation of another type. It may be better to clone stub
                    // type and separate instances for separate source type, but for now we'll just avoid stomping
                    // on the existing documentation. 
                    if (string.IsNullOrEmpty(dst.Documentation)) {
                        var srcDocumentation = src.Documentation;
                        if (!string.IsNullOrEmpty(srcDocumentation)) {
                            dst.SetDocumentation(srcDocumentation);
                        }
                    }
                }

                if (src.Location.IsValid) {
                    dst.Location = src.Location;
                }
            }
        }

        /// <summary>
        /// Determines if type comes from module that is part of this package.
        /// </summary>
        /// <remarks>
        /// Single stub file typically applies to all modules while types within
        /// the package come from multiple modules. We need to determine if stub
        /// does match the type module so we don't accidentally modify documentation
        /// or location of unrelated types such as coming from the base object type.
        /// </remarks>
        private bool IsAcceptableModule(IPythonType type) {
            var thisModule = _eval.Module;
            var typeModule = type.DeclaringModule;
            var typeMainModuleName = typeModule.Name.Split('.').FirstOrDefault();
            return typeModule.Equals(thisModule) || typeMainModuleName == thisModule.Name;
        }
    }
}
