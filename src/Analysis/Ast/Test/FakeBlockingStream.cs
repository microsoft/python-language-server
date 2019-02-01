using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.Python.Analysis.Tests {

    internal class FakeBlockingStream : Stream {
        ManualResetEventSlim mres1 = new ManualResetEventSlim(false); // initialize as unsignaled
        private byte[] _internalBuffer;

        public FakeBlockingStream(byte[] buffer) {
            _internalBuffer = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, _internalBuffer, 0, buffer.Length);
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
            Buffer.BlockCopy(_internalBuffer, 0, buffer, 0, count);
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }

        public void Unblock() {
            mres1.Set();
        }
    }
}
