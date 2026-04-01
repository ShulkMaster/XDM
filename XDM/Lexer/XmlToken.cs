namespace Shulkmaster.XDM.Lexer;

public enum XmlTokenKind
{
    None,
    OpenTag,      // <
    CloseTag,     // >
    CloseTagStart, // </
    Slash,        // /
    Hyphen,       // -
    Ampersand,    // &
    Equals,       // =
    OpenBrace,    // {
    CloseBrace,   // }
    OpenBracket,  // [
    CloseBracket, // ]
    Text,         // sequence of characters that are not the above
    Identifier,   // alphanumeric
    Eof           // End of stream
}

public struct XmlToken
{
    public XmlTokenKind Kind { get; init; }
    public ReadOnlyMemory<char> Span { get; init; }
}