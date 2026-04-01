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
            foreach (var token in lexer.GetTokens())
            {
                if (token.Kind == XmlTokenKind.EOF) goto done;
                tokens.Add((token.Kind, token.Span.ToString()));
            }
            
            if (!await lexer.FetchNextChunkAsync())
            {
                // If we can't fetch more, and EOF wasn't reached, something might be wrong or we just need one more TryReadNextToken for EOF
                // but GetTokens already handles TryReadNextToken.
            }
        }
        done:;

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

        var tokens = lexer.GetTokens().ToList();
        Assert.Equal(2, tokens.Count);
        
        var t1 = tokens[0];
        Assert.Equal(XmlTokenKind.Ampersand, t1.Kind);
        Assert.Equal("&", t1.Span.ToString());

        var t2 = tokens[1];
        Assert.Equal(XmlTokenKind.Hyphen, t2.Kind);
        Assert.Equal("-", t2.Span.ToString());

        // Reader exhausted but EOS not yet set
        Assert.Empty(lexer.GetTokens());
        Assert.False(await lexer.FetchNextChunkAsync());
        
        var t3 = lexer.GetTokens().First();
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

        var tokens1 = lexer.GetTokens().ToList();
        Assert.Equal(2, tokens1.Count);
        var t1 = tokens1[0];
        Assert.Equal(XmlTokenKind.Text, t1.Kind);
        Assert.Equal(1024, t1.Span.Length);

        var t2 = tokens1[1];
        Assert.Equal(XmlTokenKind.Text, t2.Kind);
        Assert.Equal(2000 - 1024, t2.Span.Length);

        Assert.Empty(lexer.GetTokens());
        Assert.False(await lexer.FetchNextChunkAsync());
        var t3 = lexer.GetTokens().First();
        Assert.Equal(XmlTokenKind.EOF, t3.Kind);
    }

    [Fact]
    public async Task SupportsCancellation()
    {
        var xml = "<a>text</a>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        var lexer = new XmlLexer(reader);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => lexer.FetchNextChunkAsync(cts.Token));
    }
}