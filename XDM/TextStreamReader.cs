using System.Buffers;
using System.Text;

namespace Shulkmaster.XDM;

public sealed class TextStreamReader : ITextStreamReader
{
    private readonly Stream _stream;
    private readonly byte[] _byteBuffer;
    private Memory<byte> _currentChunk;
    private bool _isStreamExhausted;
    private int _byteIndex;

    private bool IsEndOfStream => _isStreamExhausted && _byteIndex >= _currentChunk.Length;

    public bool EOS => IsEndOfStream;

    public TextStreamReader(Stream stream, int bufferSize = 4096)
    {
        // Ensure the buffer size is at least 4 bytes for a single UTF-8 character
        var byteSize = Math.Abs(bufferSize);
        byteSize = Math.Max(4, byteSize);

        _stream = stream;
        _byteBuffer = new byte[byteSize];
        _currentChunk = Memory<byte>.Empty;
        _byteIndex = 0;
        _isStreamExhausted = false;
    }

    public async Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default)
    {
        if (_isStreamExhausted)
        {
            return false;
        }

        var leftover = _currentChunk.Length - _byteIndex;

        if (leftover > 0 && _byteIndex > 0)
        {
            // Move leftover bytes to the beginning of the buffer
            var leftOverBytes = _currentChunk.Span[_byteIndex..];
            leftOverBytes.CopyTo(_byteBuffer);
        }

        // Read new data AFTER the leftover bytes
        var bytesToRead = _byteBuffer.Length - leftover;
        var destination = _byteBuffer.AsMemory(leftover, bytesToRead);
        _byteIndex = 0;

        var bytesRead = await _stream.ReadAsync(destination, cancellationToken);

        if (bytesRead != 0 || leftover > 0)
        {
            _currentChunk = _byteBuffer.AsMemory(0, leftover + bytesRead);
            return bytesRead > 0;
        }

        _isStreamExhausted = true;

        if (leftover != 0)
        {
            // UTF-8 decoder will return NeedMoreData if the last character is incomplete
            throw new InvalidOperationException("Stream ended unexpectedly");
        }

        _currentChunk = Memory<byte>.Empty;
        _byteIndex = 0;
        return false;
    }

    public bool TryNext(out Rune rune)
    {
        if (_byteIndex >= _currentChunk.Length)
        {
            rune = default;
            return false;
        }

        ReadOnlySpan<byte> remaining = _currentChunk.Span[_byteIndex..];
        var status = Rune.DecodeFromUtf8(remaining, out rune, out int bytesConsumed);

        switch (status)
        {
            case OperationStatus.Done:
                _byteIndex += bytesConsumed;
                return true;
            case OperationStatus.InvalidData:
                _byteIndex += 1;
                rune = Rune.ReplacementChar;
                return true;
            case OperationStatus.NeedMoreData:
                return false;
            case OperationStatus.DestinationTooSmall:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}