using System.Text;
using Shulkmaster.XDM;
using Shulkmaster.XDM.Lexer;

namespace XDM.Test.Lexer;

public class XmlLexerTests
{
    [Fact]
    public async Task BasicLexingWorks()
    {
        var xml = "<a>text</a>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream, 5); // Small chunks
        var lexer = new XmlLexer(reader);

        var tokens = new List<(XmlTokenKind, string)>();
        
        while (true)
        {
            if (lexer.TryReadNextToken(out var token))
            {
                if (token.Kind == XmlTokenKind.EOF) break;
                tokens.Add((token.Kind, token.Span.ToString()));
            }
            else
            {
                await reader.FetchNextChunkAsync();
            }
        }

        // With chunk size 5, "text" might be split into "te" and "xt"
        // <a>te (5 chars)
        // xt</a> (5 chars)
        Assert.True(tokens.Count >= 8);
        
        var reconstructed = string.Join("", tokens.Select(t => t.Item2));
        Assert.Equal(xml, reconstructed);

        Assert.Contains(tokens, t => t is { Item1: XmlTokenKind.OpenTag, Item2: "<" });
        Assert.Contains(tokens, t => t is { Item1: XmlTokenKind.Slash, Item2: "/" });
    }

    [Fact]
    public async Task HandlesEntitiesAndHyphens()
    {
        var xml = "&-";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader);

        Assert.True(lexer.TryReadNextToken(out var t1));
        Assert.Equal(XmlTokenKind.Ampersand, t1.Kind);
        Assert.Equal("&", t1.Span.ToString());

        Assert.True(lexer.TryReadNextToken(out var t2));
        Assert.Equal(XmlTokenKind.Hyphen, t2.Kind);
        Assert.Equal("-", t2.Span.ToString());

        // Reader exhausted but EOS not yet set
        Assert.False(lexer.TryReadNextToken(out _));
        Assert.False(await reader.FetchNextChunkAsync());
        
        Assert.True(lexer.TryReadNextToken(out var t3));
        Assert.Equal(XmlTokenKind.EOF, t3.Kind);
    }

    [Fact]
    public async Task HandlesLongText()
    {
        var longText = new string('x', 2000);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(longText));
        using var reader = new TextStreamReader(stream, 4000);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader, 1024);

        Assert.True(lexer.TryReadNextToken(out var t1));
        Assert.Equal(XmlTokenKind.Text, t1.Kind);
        Assert.Equal(1024, t1.Span.Length);

        Assert.True(lexer.TryReadNextToken(out var t2));
        Assert.Equal(XmlTokenKind.Text, t2.Kind);
        Assert.Equal(2000 - 1024, t2.Span.Length);

        Assert.False(lexer.TryReadNextToken(out _));
        Assert.False(await reader.FetchNextChunkAsync());
        Assert.True(lexer.TryReadNextToken(out var t3));
        Assert.Equal(XmlTokenKind.EOF, t3.Kind);
    }
}