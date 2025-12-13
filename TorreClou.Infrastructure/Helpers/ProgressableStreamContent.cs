using System.Net;

namespace TorreClou.Infrastructure.Helpers
{
    public class ProgressableStreamContent : HttpContent
    {
        private const int DefaultBufferSize = 81920; // 80 KB buffer
        private readonly Stream _content;
        private readonly int _bufferSize;
        private readonly Action<long, long> _progress;

        public ProgressableStreamContent(Stream content, Action<long, long> progress) : this(content, DefaultBufferSize, progress) { }

        public ProgressableStreamContent(Stream content, int bufferSize, Action<long, long> progress)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

            _content = content;
            _bufferSize = bufferSize;
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            var size = _content.Length;
            var uploaded = 0L;

            using (_content)
            {
                while (true)
                {
                    var length = await _content.ReadAsync(buffer, 0, buffer.Length);
                    if (length <= 0) break;

                    await stream.WriteAsync(buffer, 0, length);

                    uploaded += length;
                    _progress?.Invoke(uploaded, size);
                }
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }
}