using Document.Layout;
using Document.Template;
using Document.ViewNodes;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using Shulkmaster.XDM.Model;

namespace Document.Test;

public class TemplateBuilderTest
{
    private class TestFontResolver : IFontResolver
    {
        public byte[] GetFont(string faceName)
        {
            var path = $"/usr/share/fonts/TTF/{faceName}.ttf";
            if (File.Exists(path))
                return File.ReadAllBytes(path);

            return File.ReadAllBytes("/usr/share/fonts/TTF/DejaVuSans.ttf");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            return new FontResolverInfo("DejaVuSans");
        }
    }

    public TemplateBuilderTest()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new TestFontResolver();
    }

    [Fact]
    public void TextContent_ShouldBeSplitIntoWords()
    {
        var doc = new XmlDocument
        {
            Root = new XmlTag
            {
                Name = "document",
                Children =
                {
                    new XmlTag
                    {
                        Name = "p",
                        Children = { new XmlText { Value = "word 1 test" } }
                    }
                }
            }
        };

        var builder = new TemplateBuilder();
        var root = builder.FromXmlDoc(doc);

        // root -> <document> ViewNode -> should contain <p> ViewNode -> should contain TextViewNode
        // <document> is the root, its child is <p>, <p>'s child is the TextViewNode
        var pNode = root.Children[0];
        var textView = pNode.Children[0] as TextViewNode;

        Assert.NotNull(textView);
        Assert.Equal(3, textView.Text.Length);
        Assert.Equal("word", textView.Text[0].Text);
        Assert.Equal("1", textView.Text[1].Text);
        Assert.Equal("test", textView.Text[2].Text);
    }

    [Fact]
    public void TextXmlTemplate_WordsShouldHaveNonZeroPositions()
    {
        var doc = new XmlDocument
        {
            Root = new XmlTag
            {
                Name = "document",
                Children =
                {
                    new XmlTag
                    {
                        Name = "p",
                        Children = { new XmlText { Value = "word 2 test" } }
                    }
                }
            }
        };

        var builder = new TemplateBuilder();
        var root = builder.FromXmlDoc(doc);

        var media = new PdfMedia();
        var engine = new LayoutEngine(media);
        engine.Layout(root, XUnit.FromPoint(600), XUnit.FromPoint(800));

        var pNode = root.Children[0];
        var textView = pNode.Children[0] as TextViewNode;

        Assert.NotNull(textView);
        Assert.True(textView.Text.Length >= 3, $"Expected at least 3 words but got {textView.Text.Length}");

        // First word at X=0
        Assert.Equal(0, textView.Text[0].X.Point);
        // Second word should be to the right
        Assert.True(textView.Text[1].X.Point > 0,
            $"Second word X should be > 0 but was {textView.Text[1].X.Point}");
        // Third word even further right
        Assert.True(textView.Text[2].X.Point > textView.Text[1].X.Point,
            $"Third word X ({textView.Text[2].X.Point}) should be > second word X ({textView.Text[1].X.Point})");
    }
}