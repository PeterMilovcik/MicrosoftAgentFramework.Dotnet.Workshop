using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Workshop.Common;

/// <summary>
/// Normalizes Azure OpenAI streaming chunks that contain a null choice delta.
/// Microsoft.Extensions.AI.OpenAI 10.10.1 expects the delta to be a JSON object
/// while probing for optional reasoning content.
/// </summary>
internal static class AzureOpenAIStreamingCompatibility
{
    public static PipelineTransport Transport { get; } = CreateTransport();

    private static PipelineTransport CreateTransport()
    {
        var httpClient = new HttpClient(new NullDeltaNormalizingHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        return new HttpClientPipelineTransport(httpClient);
    }

    private sealed class NullDeltaNormalizingHandler : DelegatingHandler
    {
        public NullDeltaNormalizingHandler() : base(new SocketsHttpHandler())
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(
                    response.Content.Headers.ContentType?.MediaType,
                    "text/event-stream",
                    StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            var originalContent = response.Content;
            var originalStream = await originalContent.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var normalizedContent = new StreamContent(
                new NullDeltaNormalizingStream(originalStream, originalContent));

            foreach (var header in originalContent.Headers)
            {
                normalizedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = normalizedContent;
            return response;
        }
    }

    private sealed class NullDeltaNormalizingStream(Stream inner, IDisposable owner) : Stream
    {
        private readonly StreamReader _reader = new(
            inner,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        private byte[] _buffer = [];
        private int _bufferOffset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            while (_bufferOffset >= _buffer.Length)
            {
                var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    return 0;
                }

                _buffer = Encoding.UTF8.GetBytes(NormalizeSseLine(line) + "\n");
                _bufferOffset = 0;
            }

            var bytesToCopy = Math.Min(buffer.Length, _buffer.Length - _bufferOffset);
            _buffer.AsMemory(_bufferOffset, bytesToCopy).CopyTo(buffer);
            _bufferOffset += bytesToCopy;
            return bytesToCopy;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader.Dispose();
                owner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private static string NormalizeSseLine(string line)
        {
            const string DataPrefix = "data:";
            if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                return line;
            }

            var payload = line[DataPrefix.Length..].TrimStart();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                return line;
            }

            try
            {
                if (JsonNode.Parse(payload) is not JsonObject root ||
                    root["choices"] is not JsonArray choices)
                {
                    return line;
                }

                var changed = false;
                for (var index = 0; index < choices.Count; index++)
                {
                    if (choices[index] is not JsonObject choice)
                    {
                        choices[index] = new JsonObject { ["delta"] = new JsonObject() };
                        changed = true;
                        continue;
                    }

                    if (choice["delta"] is not JsonObject)
                    {
                        choice["delta"] = new JsonObject();
                        changed = true;
                    }
                }

                return changed ? $"{DataPrefix} {root.ToJsonString()}" : line;
            }
            catch (JsonException)
            {
                return line;
            }
        }
    }
}
