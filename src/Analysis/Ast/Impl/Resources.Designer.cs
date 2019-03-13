﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Python.Analysis {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Python.Analysis.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter {0} already specified..
        /// </summary>
        internal static string Analysis_ParameterAlreadySpecified {
            get {
                return ResourceManager.GetString("Analysis_ParameterAlreadySpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Parameter {0} is missing..
        /// </summary>
        internal static string Analysis_ParameterMissing {
            get {
                return ResourceManager.GetString("Analysis_ParameterMissing", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Positional arguments are not allowed after keyword argument..
        /// </summary>
        internal static string Analysis_PositionalArgumentAfterKeyword {
            get {
                return ResourceManager.GetString("Analysis_PositionalArgumentAfterKeyword", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Too many function arguments..
        /// </summary>
        internal static string Analysis_TooManyFunctionArguments {
            get {
                return ResourceManager.GetString("Analysis_TooManyFunctionArguments", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Too many positional arguments before &apos;*&apos; argument..
        /// </summary>
        internal static string Analysis_TooManyPositionalArgumentBeforeStar {
            get {
                return ResourceManager.GetString("Analysis_TooManyPositionalArgumentBeforeStar", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unknown parameter name..
        /// </summary>
        internal static string Analysis_UnknownParameterName {
            get {
                return ResourceManager.GetString("Analysis_UnknownParameterName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analyzing in background, {0} items left....
        /// </summary>
        internal static string AnalysisProgress {
            get {
                return ResourceManager.GetString("AnalysisProgress", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, statements separated by semicolons are moved onto individual lines. If unchecked, lines with multiple statements are not modified..
        /// </summary>
        internal static string BreakMultipleStatementsPerLineLong {
            get {
                return ResourceManager.GetString("BreakMultipleStatementsPerLineLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Place statements on separate lines.
        /// </summary>
        internal static string BreakMultipleStatementsPerLineShort {
            get {
                return ResourceManager.GetString("BreakMultipleStatementsPerLineShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Documentation is still being calculated, please try again soon..
        /// </summary>
        internal static string CalculatingDocumentation {
            get {
                return ResourceManager.GetString("CalculatingDocumentation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; may not be callable.
        /// </summary>
        internal static string ErrorNotCallable {
            get {
                return ResourceManager.GetString("ErrorNotCallable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to object may not be callable.
        /// </summary>
        internal static string ErrorNotCallableEmpty {
            get {
                return ResourceManager.GetString("ErrorNotCallableEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Relative import &apos;{0}&apos; beyond top-level package.
        /// </summary>
        internal static string ErrorRelativeImportBeyondTopLevel {
            get {
                return ResourceManager.GetString("ErrorRelativeImportBeyondTopLevel", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to unresolved import &apos;{0}&apos;.
        /// </summary>
        internal static string ErrorUnresolvedImport {
            get {
                return ResourceManager.GetString("ErrorUnresolvedImport", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; used before definition.
        /// </summary>
        internal static string ErrorUseBeforeDef {
            get {
                return ResourceManager.GetString("ErrorUseBeforeDef", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The number of empty lines to include between class or function declarations at the top level of a module..
        /// </summary>
        internal static string LinesBetweenLevelDeclarationsLong {
            get {
                return ResourceManager.GetString("LinesBetweenLevelDeclarationsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Lines between top-level declarations.
        /// </summary>
        internal static string LinesBetweenLevelDeclarationsShort {
            get {
                return ResourceManager.GetString("LinesBetweenLevelDeclarationsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The number of empty lines to insert between method or class declarations within a class..
        /// </summary>
        internal static string LinesBetweenMethodsInClassLong {
            get {
                return ResourceManager.GetString("LinesBetweenMethodsInClassLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Lines between class member declarations.
        /// </summary>
        internal static string LinesBetweenMethodsInClassShort {
            get {
                return ResourceManager.GetString("LinesBetweenMethodsInClassShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to property of type {0}.
        /// </summary>
        internal static string PropertyOfType {
            get {
                return ResourceManager.GetString("PropertyOfType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to property of unknown type.
        /// </summary>
        internal static string PropertyOfUnknownType {
            get {
                return ResourceManager.GetString("PropertyOfUnknownType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, removes blank lines between methods and inserts the number specified below. Otherwise, lines between methods are not modified..
        /// </summary>
        internal static string RemoveExtraLinesBetweenMethodsLong {
            get {
                return ResourceManager.GetString("RemoveExtraLinesBetweenMethodsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Remove blank lines between methods.
        /// </summary>
        internal static string RemoveExtraLinesBetweenMethodsShort {
            get {
                return ResourceManager.GetString("RemoveExtraLinesBetweenMethodsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, semicolons at the end of lines will be removed. If unchecked, unnecessary semicolons are not modified..
        /// </summary>
        internal static string RemoveTrailingSemicolonsLong {
            get {
                return ResourceManager.GetString("RemoveTrailingSemicolonsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Remove unnecessary semicolons.
        /// </summary>
        internal static string RemoveTrailingSemicolonsShort {
            get {
                return ResourceManager.GetString("RemoveTrailingSemicolonsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, import statements with multiple modules are separated onto individual lines. If unchecked, import statements with multiple modules are not modified..
        /// </summary>
        internal static string ReplaceMultipleImportsWithMultipleStatementsLong {
            get {
                return ResourceManager.GetString("ReplaceMultipleImportsWithMultipleStatementsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Place imported modules on new line.
        /// </summary>
        internal static string ReplaceMultipleImportsWithMultipleStatementsShort {
            get {
                return ResourceManager.GetString("ReplaceMultipleImportsWithMultipleStatementsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added before and after &apos;-&gt;&apos; operators in function definitions. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceAroundAnnotationArrowLong {
            get {
                return ResourceManager.GetString("SpaceAroundAnnotationArrowLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space before and after return annotation operators.
        /// </summary>
        internal static string SpaceAroundAnnotationArrowShort {
            get {
                return ResourceManager.GetString("SpaceAroundAnnotationArrowShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added before and after &apos;=&apos; operators in function definitions. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceAroundDefaultValueEqualsLong {
            get {
                return ResourceManager.GetString("SpaceAroundDefaultValueEqualsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert spaces around &apos;=&apos; in default parameter values.
        /// </summary>
        internal static string SpaceAroundDefaultValueEqualsShort {
            get {
                return ResourceManager.GetString("SpaceAroundDefaultValueEqualsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added between the name and opening parenthesis of the argument list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceBeforeCallParenLong {
            get {
                return ResourceManager.GetString("SpaceBeforeCallParenLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space between the function name and argument list in calls.
        /// </summary>
        internal static string SpaceBeforeCallParenShort {
            get {
                return ResourceManager.GetString("SpaceBeforeCallParenShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added between the name and opening parenthesis of the bases list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceBeforeClassDeclarationParenLong {
            get {
                return ResourceManager.GetString("SpaceBeforeClassDeclarationParenLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space between a class name and bases list.
        /// </summary>
        internal static string SpaceBeforeClassDeclarationParenShort {
            get {
                return ResourceManager.GetString("SpaceBeforeClassDeclarationParenShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added between the name and opening parenthesis of the parameter list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceBeforeFunctionDeclarationParenLong {
            get {
                return ResourceManager.GetString("SpaceBeforeFunctionDeclarationParenLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space between a function name and parameter list in declarations.
        /// </summary>
        internal static string SpaceBeforeFunctionDeclarationParenShort {
            get {
                return ResourceManager.GetString("SpaceBeforeFunctionDeclarationParenShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added before an open square bracket. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceBeforeIndexBracketLong {
            get {
                return ResourceManager.GetString("SpaceBeforeIndexBracketLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space before open square bracket.
        /// </summary>
        internal static string SpaceBeforeIndexBracketShort {
            get {
                return ResourceManager.GetString("SpaceBeforeIndexBracketShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added before and after &apos;=&apos; operators in assignments. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpacesAroundAssignmentOperatorLong {
            get {
                return ResourceManager.GetString("SpacesAroundAssignmentOperatorLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert spaces around assignments.
        /// </summary>
        internal static string SpacesAroundAssignmentOperatorShort {
            get {
                return ResourceManager.GetString("SpacesAroundAssignmentOperatorShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added before and after binary operators. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpacesAroundBinaryOperatorsLong {
            get {
                return ResourceManager.GetString("SpacesAroundBinaryOperatorsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert spaces around binary operators.
        /// </summary>
        internal static string SpacesAroundBinaryOperatorsShort {
            get {
                return ResourceManager.GetString("SpacesAroundBinaryOperatorsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added between the open square bracket and the close square bracket. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpacesWithinEmptyListExpressionLong {
            get {
                return ResourceManager.GetString("SpacesWithinEmptyListExpressionLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within empty square brackets.
        /// </summary>
        internal static string SpacesWithinEmptyListExpressionShort {
            get {
                return ResourceManager.GetString("SpacesWithinEmptyListExpressionShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open square bracket and before the close square bracket of the list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpacesWithinListExpressionLong {
            get {
                return ResourceManager.GetString("SpacesWithinListExpressionLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert spaces within square brackets of lists.
        /// </summary>
        internal static string SpacesWithinListExpressionShort {
            get {
                return ResourceManager.GetString("SpacesWithinListExpressionShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of the tuple. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpacesWithinParenthesisedTupleExpressionLong {
            get {
                return ResourceManager.GetString("SpacesWithinParenthesisedTupleExpressionLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within tuple parentheses.
        /// </summary>
        internal static string SpacesWithinParenthesisedTupleExpressionShort {
            get {
                return ResourceManager.GetString("SpacesWithinParenthesisedTupleExpressionShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of an expression. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpacesWithinParenthesisExpressionLong {
            get {
                return ResourceManager.GetString("SpacesWithinParenthesisExpressionLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within parentheses of expression.
        /// </summary>
        internal static string SpacesWithinParenthesisExpressionShort {
            get {
                return ResourceManager.GetString("SpacesWithinParenthesisExpressionShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of an argument list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinCallParensLong {
            get {
                return ResourceManager.GetString("SpaceWithinCallParensLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within argument list parentheses.
        /// </summary>
        internal static string SpaceWithinCallParensShort {
            get {
                return ResourceManager.GetString("SpaceWithinCallParensShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of a bases list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinClassDeclarationParensLong {
            get {
                return ResourceManager.GetString("SpaceWithinClassDeclarationParensLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within bases list parentheses.
        /// </summary>
        internal static string SpaceWithinClassDeclarationParensShort {
            get {
                return ResourceManager.GetString("SpaceWithinClassDeclarationParensShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of an empty bases list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinEmptyBaseClassListLong {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyBaseClassListLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within empty bases list parentheses.
        /// </summary>
        internal static string SpaceWithinEmptyBaseClassListShort {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyBaseClassListShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of an empty argument list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinEmptyCallArgumentListLong {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyCallArgumentListLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within empty argument list parentheses.
        /// </summary>
        internal static string SpaceWithinEmptyCallArgumentListShort {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyCallArgumentListShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of an empty parameter list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinEmptyParameterListLong {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyParameterListLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within empty parameter list parentheses.
        /// </summary>
        internal static string SpaceWithinEmptyParameterListShort {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyParameterListShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of an empty tuple. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinEmptyTupleExpressionLong {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyTupleExpressionLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within empty tuple parentheses.
        /// </summary>
        internal static string SpaceWithinEmptyTupleExpressionShort {
            get {
                return ResourceManager.GetString("SpaceWithinEmptyTupleExpressionShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open parenthesis and before the close parenthesis of a parameter list. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinFunctionDeclarationParensLong {
            get {
                return ResourceManager.GetString("SpaceWithinFunctionDeclarationParensLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within parameter list parentheses.
        /// </summary>
        internal static string SpaceWithinFunctionDeclarationParensShort {
            get {
                return ResourceManager.GetString("SpaceWithinFunctionDeclarationParensShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, a space is added after the open square bracket and before the close square bracket. If unchecked, spaces are removed. Otherwise, spaces are not modified..
        /// </summary>
        internal static string SpaceWithinIndexBracketsLong {
            get {
                return ResourceManager.GetString("SpaceWithinIndexBracketsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Insert space within square brackets.
        /// </summary>
        internal static string SpaceWithinIndexBracketsShort {
            get {
                return ResourceManager.GetString("SpaceWithinIndexBracketsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If checked, comments are wrapped to the specified width. If unchecked, comments are not modified..
        /// </summary>
        internal static string WrapCommentsLong {
            get {
                return ResourceManager.GetString("WrapCommentsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to # Not wrapped:
        ///# There should be one-- and preferably only one --obvious way to do it..
        /// </summary>
        internal static string WrapCommentsLong_Example {
            get {
                return ResourceManager.GetString("WrapCommentsLong_Example", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wrap comments that are too wide.
        /// </summary>
        internal static string WrapCommentsShort {
            get {
                return ResourceManager.GetString("WrapCommentsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to # Wrapped to 40 columns:
        ///# There should be one-- and preferably
        ///# only one --obvious way to do it..
        /// </summary>
        internal static string WrapCommentsShort_Example {
            get {
                return ResourceManager.GetString("WrapCommentsShort_Example", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to # Sets the width for wrapping comments
        ///# and documentation strings..
        /// </summary>
        internal static string WrappingWidth_Doc {
            get {
                return ResourceManager.GetString("WrappingWidth_Doc", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The number of the last column that should include comment text. Words after this column are moved to the following line..
        /// </summary>
        internal static string WrappingWidthLong {
            get {
                return ResourceManager.GetString("WrappingWidthLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Maximum comment width.
        /// </summary>
        internal static string WrappingWidthShort {
            get {
                return ResourceManager.GetString("WrappingWidthShort", resourceCulture);
            }
        }
    }
}
