using System.Collections;
using System.Text;

namespace Shulkmaster.XDM;

public sealed class TextStreamReader : ITextStreamReader
{
    private readonly Stream _stream;
    private readonly Decoder _decoder;
    private readonly byte[] _byteBuffer;
    private readonly char[] _charBuffer;
    private int _charCount;
    private int _charIndex;
    private bool _isStreamExhausted;

    private bool IsEndOfStream => _isStreamExhausted && _charIndex >= _charCount;
    public bool EOS => IsEndOfStream;

    public TextStreamReader(Stream stream, int bufferSize = 4096)
    {
        _stream = stream;
        _decoder = Encoding.UTF8.GetDecoder();
        _byteBuffer = new byte[bufferSize];
        _charBuffer = new char[Encoding.UTF8.GetMaxCharCount(bufferSize)];
        _charCount = 0;
        _charIndex = 0;
        _isStreamExhausted = false;
    }

    public async Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default)
    {
        if (_isStreamExhausted)
        {
            return false;
        }

        var memory = _byteBuffer.AsMemory(0, _byteBuffer.Length);
        var bytesRead = await _stream.ReadAsync(memory, cancellationToken);
        
        if (bytesRead == 0)
        {
            _isStreamExhausted = true;
            _charCount = _decoder.GetChars([], 0, 0, _charBuffer, 0, true);
            _charIndex = 0;
            return _charCount > 0;
        }

        _charCount = _decoder.GetChars(_byteBuffer, 0, bytesRead, _charBuffer, 0, false);
        _charIndex = 0;
        
        return true;
    }

    public IEnumerator<char> GetEnumerator()
    {
        while (_charIndex < _charCount)
        {
            yield return _charBuffer[_charIndex++];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        _stream.Dispose();
    }
}