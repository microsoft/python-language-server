using Microsoft.PythonTools.Parsing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.Indexing {
    class ParseProvider : IParseProvider {
        private PythonLanguageVersion _pythonLanguageVersion;
        private ConcurrentDictionary<Uri, List<IParseObserver>> _observers = new ConcurrentDictionary<Uri, List<IParseObserver>>();

        public ParseProvider(PythonLanguageVersion pythonLanguageVersion) {
            _pythonLanguageVersion = pythonLanguageVersion;
        }

        public void RefreshAst(Uri uri) {
            new Task(() => {
                using (var stream = new StreamReader(uri.AbsolutePath)) {
                    var parser = Parser.CreateParser(stream, _pythonLanguageVersion);
                    var pythonAst = parser.ParseFile();
                    foreach (var observer in _observers[uri]) {
                        observer.UpdateParseTree(uri, pythonAst);
                    }
                }
            }).Start();
        }

        public void RefreshAstDoc(Uri uri, IDocument doc) {
            new Task(() => {
                var parser = Parser.CreateParser(doc.ReadDocument(0, out var version), _pythonLanguageVersion);
                var pythonAst = parser.ParseFile();
                foreach (var observer in _observers[uri]) {
                    observer.UpdateParseTree(uri, pythonAst);
                }
            }).Start();
            
        }
    
        public void SubscribeAst(Uri uri, IParseObserver observer) {
            if (!_observers.ContainsKey(uri)) {
                _observers[uri] = new List<IParseObserver>();
            }
            _observers[uri].Add(observer);
            RefreshAst(uri);
        }
    }
}
