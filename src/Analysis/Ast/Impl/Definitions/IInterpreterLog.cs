using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Python.Analysis {
    public interface IInterpreterLog {
        void Log(string msg);
    }
}
