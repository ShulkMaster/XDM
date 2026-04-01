namespace Shulkmaster.XDM.Lexer;

public class XmlLexer
{
    private readonly ITextStreamReader _reader;
    private IEnumerator<char>? _enumerator;
    private bool _hasPeeked;
    private char _peeked;
    private readonly char[] _tokenBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlLexer"/> class.
    /// </summary>
    /// <param name="reader">The text stream reader to lex from.</param>
    /// <param name="bufferSize">The size of the internal buffer used for tokens like Text. Default is 1024.</param>
    /// <remarks>
    /// For performance, you might consider using <see cref="System.Buffers.ArrayPool{T}"/> for the internal buffer
    /// if the lexer is short-lived. For long-term storage of text, consider a chunked buffer strategy
    /// (e.g., <see cref="System.Collections.Generic.List{T}"/> of char arrays) to avoid large object heap
    /// issues and reallocations inherent to <see cref="System.Text.StringBuilder"/>.
    /// </remarks>
    public XmlLexer(ITextStreamReader reader, int bufferSize = 1024)
    {
        _reader = reader;
        _tokenBuffer = new char[bufferSize];
    }

    /// <summary>
    /// Returns an enumerable of tokens that can be read from the current chunk.
    /// </summary>
    /// <returns>An enumerable of tokens.</returns>
    public IEnumerable<XmlToken> GetTokens()
    {
        while (true)
        {
            if (_enumerator == null)
            {
                _enumerator = _reader.GetEnumerator();
            }

            if (!_hasPeeked)
            {
                if (!_enumerator.MoveNext())
                {
                    _enumerator = null;
                    if (_reader.EOS)
                    {
                        yield return new XmlToken { Kind = XmlTokenKind.EOF, Span = ReadOnlyMemory<char>.Empty };
                    }

                    yield break;
                }

                _peeked = _enumerator.Current;
                _hasPeeked = true;
            }

            char c = _peeked;
            if (IsIdentity(c))
            {
                _hasPeeked = false;
                yield return new XmlToken { Kind = GetKind(c), Span = GetConstantMemory(c) };
                continue;
            }

            // Text
            int count = 0;
            _tokenBuffer[count++] = c;
            _hasPeeked = false;

            while (_enumerator.MoveNext())
            {
                char next = _enumerator.Current;
                if (IsIdentity(next))
                {
                    _peeked = next;
                    _hasPeeked = true;
                    break;
                }

                if (count < _tokenBuffer.Length)
                {
                    _tokenBuffer[count++] = next;
                }
                else
                {
                    // Buffer is full, yield what we have and keep the next char for the next token
                    _peeked = next;
                    _hasPeeked = true;
                    break;
                }
            }

            if (!_hasPeeked)
            {
                // We exhausted the current chunk without finding an identity character
                _enumerator = null;
            }

            yield return new XmlToken { Kind = XmlTokenKind.Text, Span = _tokenBuffer.AsMemory(0, count) };
        }
    }

    private static bool IsIdentity(char c) => c is '<' or '>' or '/' or '&' or '-';

    private static XmlTokenKind GetKind(char c) => c switch
    {
        '<' => XmlTokenKind.OpenTag,
        '>' => XmlTokenKind.CloseTag,
        '/' => XmlTokenKind.Slash,
        '&' => XmlTokenKind.Ampersand,
        '-' => XmlTokenKind.Hyphen,
        _ => XmlTokenKind.None
    };

    private static ReadOnlyMemory<char> GetConstantMemory(char c) => c switch
    {
        '<' => "<".AsMemory(),
        '>' => ">".AsMemory(),
        '/' => "/".AsMemory(),
        '&' => "&".AsMemory(),
        '-' => "-".AsMemory(),
        _ => "".AsMemory()
    };
    /// <summary>
    /// Fetches the next chunk of data from the underlying reader.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if more data was successfully fetched; otherwise, false.</returns>
    public Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default)
    {
        return _reader.FetchNextChunkAsync(cancellationToken);
    }
}