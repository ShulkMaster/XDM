using Moq;
using Shulkmaster.XDM.Lexer;
using Shulkmaster.XDM.Model;
using Shulkmaster.XDM.Parser;
using Shulkmaster.XDM.Expressions;
using XDM.Test.Lexer;

namespace XDM.Test.Parser;

public class XmlParserTests
{
    private static IXmlLexer CreateMockLexer(IEnumerable<XmlToken> tokens)
    {
        var moq = new Mock<IXmlLexer>();

        moq.Setup(lexer => lexer.GetTokens())
            .Returns(tokens);

        return moq.Object;
    }

    [Fact]
    public async Task BasicXmlParsingWorks()
    {
        var b = new XmlTokenBuilder();
        b.OpenTag("foo")
            .Attribute("attr", "val")
            .CloseTag()
                .Text("text")
            .EndTag();
        var tokens = b.Build();
        var lexer = CreateMockLexer(tokens);
        var parser = new XmlParser(lexer);

        var doc = await parser.ParseAsync();

        Assert.True(doc.Finished);
        Assert.NotNull(doc.Root);

        // Root is virtual, so it should have one child 'foo'
        Assert.Single(doc.Root.Children);
        var foo = doc.Root.Children[0] as XmlTag;
        Assert.NotNull(foo);
        Assert.Equal("foo", foo.Name);

        Assert.Single(foo.Attributes);
        Assert.Equal("attr", foo.Attributes[0].Name);
        var attrVal = foo.Attributes[0].Value as StringExpression;
        Assert.NotNull(attrVal);
        Assert.Equal("val", attrVal.Value);

        Assert.Single(foo.Children);
        var text = foo.Children[0] as XmlText;
        Assert.Equal("text", text!.Value);
    }
}