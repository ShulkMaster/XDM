namespace Shulkmaster.XDM.Lexer;

public enum XmlTokenKind
{
    None,
    OpenTag,      // <
    CloseTag,     // >
    Slash,        // /
    Hyphen,       // -
    Ampersand,    // &
    Text,         // sequence of characters that are not the above
    Eoc,          // End of chunk
    Eof           // End of stream
}

public struct XmlToken
{
    public XmlTokenKind Kind { get; init; }
    public ReadOnlyMemory<char> Span { get; init; }
}