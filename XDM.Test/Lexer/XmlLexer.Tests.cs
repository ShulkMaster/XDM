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
        
        var continueReading = true;
        while (continueReading)
        {
            foreach (var token in lexer.GetTokens())
            {
                if (token.Kind == XmlTokenKind.Eof)
                {
                    continueReading = false;
                    break;
                }
                tokens.Add((token.Kind, token.Span.ToString()));
            }
            
            if (!await lexer.FetchNextChunkAsync())
            {
                // If we can't fetch more, and EOF wasn't reached, something might be wrong or we just need one more TryReadNextToken for EOF
                // but GetTokens already handles TryReadNextToken.
            }
        }

        // With the new state machine, it will reconstruct to 7 tokens
        // < (OpenTag), a (Identifier), > (CloseTag), text (Identifier), </ (CloseTagStart), a (Identifier), > (CloseTag)
        Assert.True(tokens.Count >= 7);
        
        var reconstructed = string.Join("", tokens.Select(t => t.Item2));
        Assert.Equal(xml, reconstructed);

        Assert.Contains(tokens, t => t is { Item1: XmlTokenKind.OpenTag, Item2: "<" });
        Assert.Contains(tokens, t => t is { Item1: XmlTokenKind.CloseTagStart, Item2: "</" });
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
        Assert.Equal(XmlTokenKind.Eof, t3.Kind);
    }

    [Fact]
    public async Task HandlesLongText()
    {
        var longText = new string('x', 2000);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(longText));
        using var reader = new TextStreamReader(stream, 4000);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader);

        // It should not yield tokens yet, because identifier continues
        var tokens1 = lexer.GetTokens().ToList();
        Assert.Empty(tokens1);

        // Reach EOS
        Assert.False(await lexer.FetchNextChunkAsync());
        
        // Now it should yield the identifier and Eof
        var tokens2 = lexer.GetTokens().ToList();
        Assert.Equal(2, tokens2.Count);
        var t1 = tokens2[0];
        Assert.Equal(XmlTokenKind.Identifier, t1.Kind);
        Assert.Equal(2000, t1.Span.Length);

        var t2 = tokens2[1];
        Assert.Equal(XmlTokenKind.Eof, t2.Kind);
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

    [Fact]
    public async Task HandlesEmojisInText()
    {
        var xml = "<a>🌍</a>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader);

        var tokens = lexer.GetTokens().ToList();
        // <, a, >, 🌍, </, a, >
        Assert.Equal(7, tokens.Count);
        
        Assert.Equal(XmlTokenKind.OpenTag, tokens[0].Kind);
        Assert.Equal(XmlTokenKind.Identifier, tokens[1].Kind);
        Assert.Equal("a", tokens[1].Span.ToString());
        Assert.Equal(XmlTokenKind.CloseTag, tokens[2].Kind);
        Assert.Equal(XmlTokenKind.Text, tokens[3].Kind);
        Assert.Equal("🌍", tokens[3].Span.ToString());
        Assert.Equal(XmlTokenKind.CloseTagStart, tokens[4].Kind);
        Assert.Equal(XmlTokenKind.Identifier, tokens[5].Kind);
        Assert.Equal("a", tokens[5].Span.ToString());
        Assert.Equal(XmlTokenKind.CloseTag, tokens[6].Kind);
    }

    [Fact]
    public async Task HandlesSelfClosingTags()
    {
        var xml = "<a />";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader);

        var tokens = lexer.GetTokens().ToList();
        // <, a, " ", /, >
        Assert.Equal(5, tokens.Count);
        Assert.Equal(XmlTokenKind.OpenTag, tokens[0].Kind);
        Assert.Equal(XmlTokenKind.Identifier, tokens[1].Kind);
        Assert.Equal("a", tokens[1].Span.ToString());
        Assert.Equal(XmlTokenKind.Text, tokens[2].Kind);
        Assert.Equal(" ", tokens[2].Span.ToString());
        Assert.Equal(XmlTokenKind.Slash, tokens[3].Kind);
        Assert.Equal(XmlTokenKind.CloseTag, tokens[4].Kind);
    }

    [Fact]
    public async Task HandlesSplitCloseTagStart()
    {
        var xml = "<a>text</a>";
        // <a>text is 7 chars.
        // If we use chunk size 7, the first chunk is "<a>text"
        // Wait, length is 11.
        // <a>text (7)
        // </a> (4)
        // Wait, if chunk size is 8:
        // <a>text< (8)
        // /a> (3)
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream, 8);
        var lexer = new XmlLexer(reader);

        var tokens = new List<XmlToken>();
        while (await lexer.FetchNextChunkAsync() || !reader.EOS)
        {
            tokens.AddRange(lexer.GetTokens());
        }
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.CloseTagStart);
    }

    [Fact]
    public async Task HandlesBindingAttributesAndInterpolation()
    {
        var xml = "<Person age={30} name={exp}>{{escaped}}</Person>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader);

        var tokens = lexer.GetTokens().ToList();

        // Let's verify some key tokens
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.OpenBrace);
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.CloseBrace);
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.Equals);
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.Identifier && t.Span.ToString() == "exp");

        var reconstructed = string.Join("", tokens.Select(t => t.Span.ToString()));
        Assert.Equal("<Person age={30} name={exp}>{escaped}</Person>", reconstructed);
    }

    [Theory]
    [InlineData("{13}", 13.0)]
    [InlineData("{0.4}", 0.4)]
    [InlineData("{.15}", 0.15)]
    [InlineData("{100}", 100.0)]
    [InlineData("{3.14159}", 3.14159)]
    [InlineData("{1_000}", 1000.0)]
    [InlineData("{1_000.50}", 1000.50)]
    [InlineData("{.5}", 0.5)]
    public async Task HandlesNumberBindings(string input, double expectedValue)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using var reader = new TextStreamReader(stream);
        var lexer = new XmlLexer(reader);

        var tokens = await CollectTokens(lexer);

        // { number }
        Assert.True(tokens.Count >= 3);
        Assert.Equal(XmlTokenKind.OpenBrace, tokens[0].Kind);
        Assert.Equal(XmlTokenKind.NumberLiteral, tokens[1].Kind);
        Assert.Equal(expectedValue, tokens[1].NumberValue, 10);
        Assert.Equal(XmlTokenKind.CloseBrace, tokens[2].Kind);
    }

    [Fact]
    public async Task HandlesNumberInAttributeBinding()
    {
        var xml = "<div width={13} />";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        var lexer = new XmlLexer(reader);

        var tokens = await CollectTokens(lexer);

        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.NumberLiteral && t.NumberValue == 13.0);
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.Identifier && t.Span.ToString() == "width");
        Assert.Contains(tokens, t => t.Kind == XmlTokenKind.Equals);
    }

    [Fact]
    public async Task HandlesDecimalNumberInAttributeBinding()
    {
        var xml = "<div borderWidth={0.4} />";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        var lexer = new XmlLexer(reader);

        var tokens = await CollectTokens(lexer);

        var numToken = tokens.First(t => t.Kind == XmlTokenKind.NumberLiteral);
        Assert.Equal(0.4, numToken.NumberValue, 10);
    }

    [Fact]
    public async Task HandlesFractionalNumberInAttributeBinding()
    {
        var xml = "<div width={.15} />";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        var lexer = new XmlLexer(reader);

        var tokens = await CollectTokens(lexer);

        var numToken = tokens.First(t => t.Kind == XmlTokenKind.NumberLiteral);
        Assert.Equal(0.15, numToken.NumberValue, 10);
    }

    [Fact]
    public async Task HandlesUnderscoreSeparatorsInNumbers()
    {
        var xml = "{1_000_000}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        var lexer = new XmlLexer(reader);

        var tokens = await CollectTokens(lexer);

        var numToken = tokens.First(t => t.Kind == XmlTokenKind.NumberLiteral);
        Assert.Equal(1000000.0, numToken.NumberValue, 10);
        Assert.Equal("1000000", numToken.Span.ToString());
    }

    private static async Task<List<XmlToken>> CollectTokens(XmlLexer lexer)
    {
        var tokens = new List<XmlToken>();
        var continueReading = true;
        while (continueReading)
        {
            foreach (var token in lexer.GetTokens())
            {
                if (token.Kind == XmlTokenKind.Eof)
                {
                    continueReading = false;
                    break;
                }
                tokens.Add(token);
            }

            if (continueReading && !await lexer.FetchNextChunkAsync())
            {
            }
        }
        return tokens;
    }
}