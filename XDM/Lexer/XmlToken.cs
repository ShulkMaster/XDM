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
    EOF           // End of stream
}

public ref struct XmlToken
{
    public XmlTokenKind Kind { get; init; }
    public ReadOnlySpan<char> Span { get; init; }
}