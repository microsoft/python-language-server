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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class AssertionsFactory {
        public static BoundBuiltinMethodInfoAssertions Should(this AnalysisValueTestInfo<BoundBuiltinMethodInfo> testInfo)
            => new BoundBuiltinMethodInfoAssertions(testInfo);

        public static BuiltinClassInfoAssertions Should(this AnalysisValueTestInfo<IBuiltinClassInfo> testInfo)
            => new BuiltinClassInfoAssertions(testInfo);

        public static BuiltinInstanceInfoAssertions Should(this AnalysisValueTestInfo<IBuiltinInstanceInfo> testInfo)
            => new BuiltinInstanceInfoAssertions(testInfo);

        public static BuiltinFunctionInfoAssertions Should(this AnalysisValueTestInfo<BuiltinFunctionInfo> testInfo)
            => new BuiltinFunctionInfoAssertions(testInfo);

        public static BuiltinModuleAssertions Should(this AnalysisValueTestInfo<BuiltinModule> testInfo)
            => new BuiltinModuleAssertions(testInfo);

        public static ClassInfoAssertions Should(this AnalysisValueTestInfo<IClassInfo> testInfo)
            => new ClassInfoAssertions(testInfo);

        public static DictionaryInfoAssertions Should(this AnalysisValueTestInfo<DictionaryInfo> testInfo)
            => new DictionaryInfoAssertions(testInfo);

        public static FunctionInfoAssertions Should(this AnalysisValueTestInfo<IFunctionInfo> testInfo)
            => new FunctionInfoAssertions(testInfo);

        public static InstanceInfoAssertions Should(this AnalysisValueTestInfo<IInstanceInfo> testInfo)
            => new InstanceInfoAssertions(testInfo);

        public static ModuleInfoAssertions Should(this AnalysisValueTestInfo<IModuleInfo> testInfo)
            => new ModuleInfoAssertions(testInfo);

        public static ParameterInfoAssertions Should(this AnalysisValueTestInfo<ParameterInfo> testInfo)
            => new ParameterInfoAssertions(testInfo);

        public static ProtocolInfoAssertions Should(this AnalysisValueTestInfo<ProtocolInfo> testInfo)
            => new ProtocolInfoAssertions(testInfo);

        public static PythonPackageAssertions Should(this AnalysisValueTestInfo<PythonPackage> testInfo)
            => new PythonPackageAssertions(testInfo);

        public static SequenceInfoAssertions Should(this AnalysisValueTestInfo<SequenceInfo> testInfo)
            => new SequenceInfoAssertions(testInfo);

        public static SpecializedCallableAssertions Should(this AnalysisValueTestInfo<SpecializedCallable> specializedCallable)
            => new SpecializedCallableAssertions(specializedCallable);

        public static AstPythonFunctionAssertions Should(this AstPythonFunction pythonFunction)
            => new AstPythonFunctionAssertions(pythonFunction);

        public static FunctionScopeAssertions Should(this IFunctionScope functionScope)
            => new FunctionScopeAssertions(functionScope);

        public static CompletionListAssertions Should(this CompletionList completionList)
            => new CompletionListAssertions(completionList);

        public static CompletionItemAssertions Should(this CompletionItem completionItem)
            => new CompletionItemAssertions(completionItem);

        public static ScopeAssertions Should(this IScope scope)
            => new ScopeAssertions(scope);

        public static MemberContainerAssertions<IMemberContainer> Should(this IMemberContainer memberContainer)
            => new MemberContainerAssertions<IMemberContainer>(memberContainer);

        public static ModuleAnalysisAssertions Should(this IModuleAnalysis moduleAnalysis)
            => new ModuleAnalysisAssertions(moduleAnalysis);

        public static HoverAssertions Should(this Hover hover)
            => new HoverAssertions(hover);

        public static ParameterResultAssertions Should(this ParameterResult overloadResult)
            => new ParameterResultAssertions(overloadResult);

        public static RangeAssertions Should(this Range? range)
            => new RangeAssertions(range);

        public static ReferenceCollectionAssertions Should(this IEnumerable<Reference> references)
            => new ReferenceCollectionAssertions(references);

        public static SignatureHelpAssertions Should(this SignatureHelp signatureHelp)
            => new SignatureHelpAssertions(signatureHelp);

        public static SignatureInformationAssertions Should(this SignatureInformation signatureInformation)
            => new SignatureInformationAssertions(signatureInformation);

        public static TextEditCollectionAssertions Should(this IEnumerable<TextEdit> textEdits)
            => new TextEditCollectionAssertions(textEdits);
    }
}
