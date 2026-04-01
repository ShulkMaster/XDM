using System.Text;

namespace Shulkmaster.XDM.Lexer;

public class XmlLexer
{
    private readonly ITextStreamReader _reader;
    private bool _hasPeeked;
    private Rune _peeked;
    private readonly char[] _tokenBuffer;

    public XmlLexer(ITextStreamReader reader, int bufferSize = 1024)
    {
        _reader = reader;
        _tokenBuffer = new char[bufferSize];
    }

    public IEnumerable<XmlToken> GetTokens()
    {
        while (true)
        {
            if (!_hasPeeked)
            {
                if (!_reader.TryNext(out _peeked))
                {
                    if (_reader.EOS)
                    {
                        yield return new XmlToken { Kind = XmlTokenKind.Eof, Span = ReadOnlyMemory<char>.Empty };
                    }

                    yield break;
                }

                _hasPeeked = true;
            }

            Rune rune = _peeked;
            if (IsIdentity(rune))
            {
                _hasPeeked = false;
                char c = (char)rune.Value;
                if (c == '<' && _reader.TryNext(out Rune next))
                {
                    if (next.IsAscii && (char)next.Value == '/')
                    {
                        yield return new XmlToken { Kind = XmlTokenKind.CloseTagStart, Span = "</".AsMemory() };
                        continue;
                    }

                    _peeked = next;
                    _hasPeeked = true;
                }
                else if (c == '<' && !_reader.EOS)
                {
                    _peeked = rune;
                    _hasPeeked = true;
                    yield break;
                }

                yield return new XmlToken { Kind = GetKind(c), Span = GetConstantMemory(c) };
                continue;
            }

            // Text
            int count = 0;
            count += rune.EncodeToUtf16(_tokenBuffer.AsSpan(count));
            _hasPeeked = false;

            while (_reader.TryNext(out Rune next))
            {
                if (IsIdentity(next))
                {
                    _peeked = next;
                    _hasPeeked = true;
                    break;
                }

                if (count + next.Utf16SequenceLength <= _tokenBuffer.Length)
                {
                    count += next.EncodeToUtf16(_tokenBuffer.AsSpan(count));
                }
                else
                {
                    // Buffer is full, yield what we have and keep the next char for the next token
                    _peeked = next;
                    _hasPeeked = true;
                    break;
                }
            }

            yield return new XmlToken { Kind = XmlTokenKind.Text, Span = _tokenBuffer.AsSpan(0, count).ToArray() };
        }
    }

    private static bool IsIdentity(Rune r) => r.IsAscii && IsIdentity((char)r.Value);

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

    public Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default)
    {
        return _reader.FetchNextChunkAsync(cancellationToken);
    }
}