namespace Shulkmaster.XDM.Lexer;

public interface IXmlLexer
{
    LexerState State { get; }
    IEnumerable<XmlToken> GetTokens();
    Task<bool> FetchNextChunkAsync(CancellationToken cancellationToken = default);
}