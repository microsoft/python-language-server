using System;
using System.IO;
using System.Threading;

namespace Microsoft.Python.Analysis.Tests {

    internal class FakeBlockingStream : Stream {
        ManualResetEventSlim mres1 = new ManualResetEventSlim(false); // initialize as unsignaled
        private MemoryStream _memoryStream;

        public FakeBlockingStream() {
            _memoryStream = new MemoryStream();
        }

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            mres1.Wait();
            return _memoryStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            _memoryStream.Write(buffer, offset, count);
            mres1.Set();
        }
    }
}
