using Shulkmaster.XDM.Lexer;
using Shulkmaster.XDM.Model;

namespace Shulkmaster.XDM.Parser;

public class XmlParser
{
    private readonly IXmlLexer _lexer;
    private readonly Stack<XmlTag> _stack = new();

    public XmlParser(IXmlLexer lexer)
    {
        _lexer = lexer;
    }


    public async Task<XmlTag> ParseAsync(CancellationToken token = default)
    {
        var root = new XmlTag();
        var current = root;
        _stack.Push(root);

        try
        {

            while (_lexer.State != LexerState.Eof)
            {
                await _lexer.FetchNextChunkAsync(token);
                var tokens = _lexer.GetTokens();
            }
        }
        catch (TaskCanceledException exception)
        {
            // no need to do anything we can return the partial parsed document
        }
        
        return root;
    }
}