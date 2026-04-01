using System.Text;
using Shulkmaster.XDM;
using Shulkmaster.XDM.Lexer;
using Shulkmaster.XDM.Model;
using Shulkmaster.XDM.Parser;
using Shulkmaster.XDM.Expressions;

namespace XDM.Test.Parser;

public class XmlParserTests
{
    [Fact]
    public async Task BasicXmlParsingWorks()
    {
        var xml = "<foo attr=\"val\">text<bar /></foo>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = new TextStreamReader(stream);
        await reader.FetchNextChunkAsync();
        var lexer = new XmlLexer(reader);
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
        
        Assert.Equal(2, foo.Children.Count);
        var text = foo.Children[0] as XmlText;
        Assert.NotNull(text);
        Assert.Equal("text", text.Value);
        
        var bar = foo.Children[1] as XmlTag;
        Assert.NotNull(bar);
        Assert.Equal("bar", bar.Name);
        Assert.Empty(bar.Children);
    }
}
