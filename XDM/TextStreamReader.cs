using System.Buffers;
using System.Collections;
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

    public IEnumerator<char> GetEnumerator()
    {
        while (_byteIndex < _currentChunk.Length)
        {
            ReadOnlySpan<byte> remaining = _currentChunk.Span.Slice(_byteIndex);

            var status = Rune.DecodeFromUtf8(remaining, out Rune rune, out int bytesConsumed);

            switch (status)
            {
                case OperationStatus.Done:
                {
                    _byteIndex += bytesConsumed;
                    // Yield the character(s) - handling surrogate pairs for characters > U+FFFF
                    if (rune.IsAscii)
                    {
                        yield return (char)rune.Value;
                    }
                    else if (rune.Value <= char.MaxValue)
                    {
                        yield return (char)rune.Value;
                    }
                    else
                    {
                        // Surrogate pair
                        yield return (char)((rune.Value - 0x10000) / 0x400 + 0xD800);
                        yield return (char)((rune.Value - 0x10000) % 0x400 + 0xDC00);
                    }

                    break;
                }
                case OperationStatus.InvalidData:
                    // Invalid UTF-8 sequence → replace and move forward by 1 byte
                    _byteIndex += 1;
                    yield return '\uFFFD'; // Unicode replacement character
                    break;
                case OperationStatus.NeedMoreData:
                    // Character is split across chunks → stop enumeration here.
                    // Consumer must fetch the next chunk and continue enumerating.
                    yield break;
                case OperationStatus.DestinationTooSmall:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        _stream.Dispose();
    }
}