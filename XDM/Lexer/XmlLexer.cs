using System.Globalization;
using System.Text;

namespace Shulkmaster.XDM.Lexer;

public sealed class XmlLexer : IXmlLexer
{
    private readonly ITextStreamReader _reader;
    private readonly StringBuilder _textBuilder = new();
    private readonly Rune[] _numberBuffer = new Rune[32];
    private int _numberLength;
    private bool _numberHasDot;
    private LexerState _state = LexerState.Default;
    
    private Rune _peek1;
    private bool _hasPeek1;
    private Rune _peek2;
    private bool _hasPeek2;
    private bool _shouldStop;
    private bool _insideTag;
    private bool _skipLeadingWhitespace;
    private bool _trimPostNewline;

    public XmlLexer(ITextStreamReader reader)
    {
        _reader = reader;
    }
    
    public LexerState State => _state;

    public IEnumerable<XmlToken> GetTokens()
    {
        _shouldStop = false;
        while (!_shouldStop)
        {
            var result = _state switch
            {
                LexerState.Default => HandleDefault(),
                LexerState.IdentitySeq => HandleIdentitySeq(),
                LexerState.TextSeq => HandleTextSeq(),
                LexerState.IdentifierSeq => HandleIdentifierSeq(),
                LexerState.NumberSeq => HandleNumberSeq(),
                LexerState.Eof => HandleEof(),
                _ => throw new ArgumentOutOfRangeException()
            };

            foreach (var token in result)
            {
                yield return token;
            }
        }
    }

    private IEnumerable<XmlToken> HandleDefault()
    {
        if (!TryPeek(out Rune r))
        {
            if (_reader.EOS)
            {
                _state = LexerState.Eof;
                yield break;
            }
            _shouldStop = true;
            yield break;
        }

        char c = (char)r.Value;

        // Skip whitespace inside tags, between tags, or at content start
        if (char.IsWhiteSpace(c) && (_insideTag || _skipLeadingWhitespace))
        {
            Consume();
            yield break;
        }

        // First non-whitespace char clears the leading-whitespace flag
        if (_skipLeadingWhitespace && !char.IsWhiteSpace(c))
        {
            _skipLeadingWhitespace = false;
        }

        if (c == '{')
        {
            if (TryPeekNext(out Rune n))
            {
                if ((char)n.Value == '{')
                {
                    Consume(); Consume();
                    _textBuilder.Append('{');
                    _state = LexerState.TextSeq;
                    yield break;
                }
            }
            else if (!_reader.EOS)
            {
                _shouldStop = true;
                yield break;
            }
        }

        if (c == '}')
        {
            if (TryPeekNext(out Rune n2))
            {
                if ((char)n2.Value == '}')
                {
                    Consume(); Consume();
                    _textBuilder.Append('}');
                    _state = LexerState.TextSeq;
                    yield break;
                }
            }
            else if (!_reader.EOS)
            {
                _shouldStop = true;
                yield break;
            }
        }

        if (c == '"')
        {
            Consume();
            _state = LexerState.TextSeq;
            yield break;
        }

        if (IsIdentity(c))
        {
            _state = LexerState.IdentitySeq;
            yield break;
        }

        if (IsNumberStart(r))
        {
            _numberLength = 0;
            _numberHasDot = false;
            _state = LexerState.NumberSeq;
            yield break;
        }
        
        if (IsIdentifierStart(r) && _insideTag)
        {
            _state = LexerState.IdentifierSeq;
            yield break;
        }

        _state = LexerState.TextSeq;
    }

    private IEnumerable<XmlToken> HandleIdentitySeq()
    {
        if (!TryPeek(out Rune rid))
        {
            // Should not happen if we transitioned from Default
            _state = LexerState.Default;
            yield break;
        }

        char cid = (char)rid.Value;
        if (cid == '<')
        {
            if (TryPeekNext(out Rune next))
            {
                if ((char)next.Value == '/')
                {
                    Consume(); Consume();
                    string s2 = CreateStringFromStack('<', '/');
                    _insideTag = true;
                    _state = LexerState.Default;
                    yield return new XmlToken { Kind = XmlTokenKind.CloseTagStart, Span = s2.AsMemory() };
                    yield break;
                }
            }
            else if (!_reader.EOS)
            {
                // We need more data to know if it's < or </
                _shouldStop = true;
                yield break;
            }
        }

        Consume();
        _state = LexerState.Default;
        var kind = GetKind(cid);

        if (kind == XmlTokenKind.OpenTag)
            _insideTag = true;
        else if (kind == XmlTokenKind.CloseTag)
        {
            _insideTag = false;
            _skipLeadingWhitespace = true;
        }

        string s1 = CreateStringFromStack(cid);
        yield return new XmlToken { Kind = kind, Span = s1.AsMemory() };
    }

    private IEnumerable<XmlToken> HandleTextSeq()
    {
        if (!TryPeek(out Rune rt))
        {
            if (_reader.EOS)
            {
                yield return YieldText();
                _state = LexerState.Eof;
                yield break;
            }
            _shouldStop = true;
            yield break;
        }

        char ct = (char)rt.Value;
        
        if (ct == '"')
        {
            Consume();
            yield return YieldText();
            _state = LexerState.Default;
            yield break;
        }

        if (ct == '{' && TryPeekNext(out Rune nt) && (char)nt.Value == '{')
        {
            Consume(); Consume();
            _textBuilder.Append('{');
            yield break;
        }

        if (ct == '}' && TryPeekNext(out Rune nt2) && (char)nt2.Value == '}')
        {
            Consume(); Consume();
            _textBuilder.Append('}');
            yield break;
        }

        if (IsIdentity(ct))
        {
            // todo: do not yield txt but rather collect all chars to figure the identity
            yield return YieldText();
            _state = LexerState.Default;
            yield break;
        }

        Consume();

        if (ct == '\n')
        {
            _textBuilder.Append('\n');
            _trimPostNewline = true;
        }
        else if (_trimPostNewline && char.IsWhiteSpace(ct))
        {
            // Skip whitespace after a newline in text content
        }
        else
        {
            _trimPostNewline = false;
            _textBuilder.Append(rt.ToString());
        }
    }

    private IEnumerable<XmlToken> HandleIdentifierSeq()
    {
        if (!TryPeek(out Rune rident))
        {
            if (_reader.EOS)
            {
                yield return YieldIdentifier();
                _state = LexerState.Eof;
                yield break;
            }
            _shouldStop = true;
            yield break;
        }

        if (IsIdentifierPart(rident))
        {
            Consume();
            _textBuilder.Append(rident.ToString());
            yield break;
        }

        yield return YieldIdentifier();
        _state = LexerState.Default;
    }

    private IEnumerable<XmlToken> HandleEof()
    {
        yield return new XmlToken { Kind = XmlTokenKind.Eof, Span = ReadOnlyMemory<char>.Empty };
        _shouldStop = true;
    }

    private XmlToken YieldText()
    {
        var text = _textBuilder.ToString();
        _textBuilder.Clear();
        return new XmlToken { Kind = XmlTokenKind.Text, Span = text.AsMemory() };
    }

    private IEnumerable<XmlToken> HandleNumberSeq()
    {
        if (!TryPeek(out Rune rn))
        {
            if (_reader.EOS)
            {
                yield return YieldNumber();
                _state = LexerState.Eof;
                yield break;
            }
            _shouldStop = true;
            yield break;
        }

        char cn = (char)rn.Value;

        if (char.IsAsciiDigit(cn))
        {
            Consume();
            if (_numberLength < 24)
                _numberBuffer[_numberLength++] = rn;
            yield break;
        }

        if (cn == '_')
        {
            Consume();
            yield break;
        }

        if (cn == '.' && !_numberHasDot)
        {
            Consume();
            _numberHasDot = true;
            if (_numberLength < 24)
                _numberBuffer[_numberLength++] = rn;
            yield break;
        }

        yield return YieldNumber();
        _state = LexerState.Default;
    }

    private XmlToken YieldNumber()
    {
        Span<char> chars = stackalloc char[_numberLength];
        for (int i = 0; i < _numberLength; i++)
            chars[i] = (char)_numberBuffer[i].Value;

        if (!double.TryParse(chars, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value))
            value = 0;

        var text = new string(chars);
        return new XmlToken { Kind = XmlTokenKind.NumberLiteral, Span = text.AsMemory(), NumberValue = value };
    }

    private XmlToken YieldIdentifier()
    {
        var ident = _textBuilder.ToString();
        _textBuilder.Clear();
        return new XmlToken { Kind = XmlTokenKind.Identifier, Span = ident.AsMemory() };
    }

    private bool TryPeek(out Rune rune)
    {
        if (_hasPeek1)
        {
            rune = _peek1;
            return true;
        }

        if (_reader.TryNext(out rune))
        {
            _peek1 = rune;
            _hasPeek1 = true;
            return true;
        }

        return false;
    }

    private bool TryPeekNext(out Rune rune)
    {
        if (_hasPeek2)
        {
            rune = _peek2;
            return true;
        }

        if (!_hasPeek1)
        {
            if (!TryPeek(out _))
            {
                rune = default;
                return false;
            }
        }

        if (_reader.TryNext(out rune))
        {
            _peek2 = rune;
            _hasPeek2 = true;
            return true;
        }

        return false;
    }

    private void Consume()
    {
        if (_hasPeek1)
        {
            if (_hasPeek2)
            {
                _peek1 = _peek2;
                _hasPeek1 = true;
                _hasPeek2 = false;
            }
            else
            {
                _hasPeek1 = false;
            }
        }
    }

    private static string CreateStringFromStack(char c)
    {
        Span<char> span = stackalloc char[1];
        span[0] = c;
        return new string(span);
    }

    private static string CreateStringFromStack(char c1, char c2)
    {
        Span<char> span = stackalloc char[2];
        span[0] = c1;
        span[1] = c2;
        return new string(span);
    }

    private static bool IsIdentity(Rune r) => r.IsAscii && IsIdentity((char)r.Value);

    private static bool IsIdentity(char c) => c is '<' or '>' or '/' or '&' or '-' or '{' or '}' or '[' or ']' or '=';

    private static bool IsIdentifierStart(Rune r) => r.IsAscii && (char.IsLetter((char)r.Value) || (char)r.Value == '_');
    
    private static bool IsIdentifierPart(Rune r) => r.IsAscii && (char.IsLetterOrDigit((char)r.Value) || (char)r.Value == '_');

    private bool IsNumberStart(Rune r)
    {
        if (!r.IsAscii) return false;
        char c = (char)r.Value;
        if (char.IsAsciiDigit(c)) return true;
        if (c == '.' && TryPeekNext(out Rune next) && next.IsAscii && char.IsAsciiDigit((char)next.Value))
            return true;
        return false;
    }

    private static XmlTokenKind GetKind(char c) => c switch
    {
        '<' => XmlTokenKind.OpenTag,
        '>' => XmlTokenKind.CloseTag,
        '/' => XmlTokenKind.Slash,
        '&' => XmlTokenKind.Ampersand,
        '-' => XmlTokenKind.Hyphen,
        '=' => XmlTokenKind.Equals,
        '{' => XmlTokenKind.OpenBrace,
        '}' => XmlTokenKind.CloseBrace,
        '[' => XmlTokenKind.OpenBracket,
        ']' => XmlTokenKind.CloseBracket,
        _ => XmlTokenKind.None
    };

    public Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default)
    {
        return _reader.FetchNextChunkAsync(cancellationToken);
    }
}