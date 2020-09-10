using System;
using System.IO;

namespace TlsConnectStressTest
{
    /// <summary>
    /// Wraps another stream and slices written data into small individually flushed writes.
    /// </summary>
    public sealed class SlicerStream : Stream
    {
        public const int PieceSize = 8;

        public SlicerStream(Stream inner)
        {
            _inner = inner;
        }

        private readonly Stream _inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            var pieces = count / PieceSize;
            if (count % PieceSize != 0)
                pieces++;

            for (var piece = 0; piece < pieces; piece++)
            {
                var innerOffset = piece * PieceSize;
                var innerCount = Math.Min(count - innerOffset, PieceSize);

                _inner.Write(buffer, offset + innerOffset, innerCount);
                _inner.Flush();
            }
        }
    }
}
