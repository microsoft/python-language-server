using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis {
    public static class ClassMemberExtensions {
        public static bool IsDunderInit(this IPythonClassMember member) {
            return member.Name == "__init__" && member.DeclaringType?.MemberType == PythonMemberType.Class;
        }

        public static bool IsDunderNew(this IPythonClassMember member) {
            return member.Name == "__new__" && member.DeclaringType?.MemberType == PythonMemberType.Class;
        }
    }
}
