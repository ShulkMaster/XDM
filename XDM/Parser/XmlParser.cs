using Shulkmaster.XDM.Lexer;
using Shulkmaster.XDM.Model;

namespace Shulkmaster.XDM.Parser;

public class XmlParser
{
    private readonly IXmlLexer _lexer;
    private readonly Stack<XmlTag> _stack = new();
    private ParserState _state = ParserState.Init;

    public XmlParser(IXmlLexer lexer)
    {
        _lexer = lexer;
    }


    public async Task<XmlDocument> ParseAsync(CancellationToken token = default)
    {
        var root = new XmlTag();
        _stack.Push(root);

        try
        {
            while (_state != ParserState.Eof)
            {
                await _lexer.FetchNextChunkAsync(token);
                if (token.IsCancellationRequested)
                {
                    break;
                }

                ConsumeTokens();
            }

            return new XmlDocument { Root = root, Finished = true };
        }
        catch (TaskCanceledException)
        {
            // no need to do anything we can return the partial parsed document
        }

        return new XmlDocument { Root = root, Finished = false };
    }

    private void ConsumeTokens()
    {
        foreach (var token in _lexer.GetTokens())
        {
            switch (token.Kind)
            {
                case XmlTokenKind.Eof:
                    _state = ParserState.Eof;
                    break;
            }
        }
    }
}