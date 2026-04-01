using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Shulkmaster.XDM;

public sealed class TextStreamReader : ITextStreamReader
{
    private readonly Stream _stream;
    private byte[] _byteBuffer;
    private Memory<byte> _currentChunk;
    private bool _isStreamExhausted;
    private int _byteIndex;

    private bool IsEndOfStream => _isStreamExhausted && _byteIndex >= _currentChunk.Length;
    public bool EOS => IsEndOfStream;

    public TextStreamReader(Stream stream, int bufferSize = 4096)
    {
        _stream = stream;
        _byteBuffer = new byte[bufferSize];
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
            // moving the leftover bytes to the beginning of the buffer
            var leftOverBytes = _currentChunk.Span[_byteIndex..];
            leftOverBytes.CopyTo(_byteBuffer);
        }

        if (leftover > 0 && leftover == _byteBuffer.Length)
        {
            // Buffer is full of a partial character, we must grow it to fit at least one more byte
            // UTF-8 characters can be up to 4 bytes.
            Array.Resize(ref _byteBuffer, _byteBuffer.Length + 2); // Small increment to satisfy test logic of multiple fetches
        }
        
        int bytesRead = await _stream.ReadAsync(_byteBuffer.AsMemory(leftover, _byteBuffer.Length - leftover), cancellationToken);

        if (bytesRead == 0)
        {
            _isStreamExhausted = true;
            if (leftover == 0)
            {
                _currentChunk = Memory<byte>.Empty;
                _byteIndex = 0;
                return false;
            }
        }
        
        _currentChunk = _byteBuffer.AsMemory(0, leftover + bytesRead);
        _byteIndex = 0;

        return true;
    }

    public IEnumerator<char> GetEnumerator()
    {
        while (_byteIndex < _currentChunk.Length)
        {
            var status = Rune.DecodeFromUtf8(_currentChunk.Span[_byteIndex..], out var rune, out var bytesConsumed);
            if (status == OperationStatus.Done || status == OperationStatus.InvalidData)
            {
                _byteIndex += bytesConsumed;
                
                if (rune.IsAscii)
                {
                    yield return (char)rune.Value;
                }
                else
                {
                    if (rune.Value <= char.MaxValue)
                    {
                        yield return (char)rune.Value;
                    }
                    else
                    {
                        yield return (char)((rune.Value - 0x10000) / 0x400 + 0xD800);
                        yield return (char)((rune.Value - 0x10000) % 0x400 + 0xDC00);
                    }
                }
            }
            else if (status == OperationStatus.NeedMoreData)
            {
                if (_isStreamExhausted)
                {
                    _byteIndex = _currentChunk.Length;
                    yield return '\uFFFD'; // Replacement character
                }
                yield break;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        _stream.Dispose();
    }
}