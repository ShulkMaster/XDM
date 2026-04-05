using Document.Layout;
using Document.ViewNodes;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace Document.Test;

public class PdfRendererTest
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

    private readonly LayoutEngine _engine;
    private readonly XFont _font;

    public PdfRendererTest()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new TestFontResolver();

        _engine = new LayoutEngine(new PdfMedia());
        _font = new XFont("DejaVu Sans", 12);
    }

    [Fact]
    public void WordPositions_ShouldBeSetByLayout()
    {
        // Two words — after layout the second word must have X > 0
        var text = new TextViewNode
        {
            Font = _font,
            Text =
            [
                new TextNode { Text = "Hello" },
                new TextNode { Text = "World" }
            ]
        };
        var root = new ViewNode { Children = { text } };

        _engine.Layout(root, XUnit.FromPoint(600), XUnit.FromPoint(800));

        // First word starts at X=0
        Assert.Equal(0, text.Text[0].X.Point);
        // Second word must be to the right of the first
        Assert.True(text.Text[1].X.Point > 0,
            $"Second word X should be > 0 but was {text.Text[1].X.Point}");

        // Both words on same line → Y should be 0
        Assert.Equal(0, text.Text[0].Y.Point);
        Assert.Equal(0, text.Text[1].Y.Point);
    }

    [Fact]
    public void WordPositions_BoundsOriginShouldBeNonZeroWhenNested()
    {
        // TextViewNode nested inside a container with padding
        var text = new TextViewNode
        {
            Font = _font,
            Text = [new TextNode { Text = "Test" }]
        };
        var container = new ViewNode
        {
            Styles = { Padding = new Padding { Start = 20, Top = 30 } },
            Children = { text }
        };
        var root = new ViewNode { Children = { container } };

        _engine.Layout(root, XUnit.FromPoint(600), XUnit.FromPoint(800));

        // The text bounds origin should reflect the padding
        Assert.True(text.Bounds.X.Point >= 20,
            $"Text Bounds.X should be >= 20 but was {text.Bounds.X.Point}");
        Assert.True(text.Bounds.Y.Point >= 30,
            $"Text Bounds.Y should be >= 30 but was {text.Bounds.Y.Point}");
    }

    [Fact]
    public void WrappedWords_ShouldHaveCorrectYPositions()
    {
        // Use a narrow container to force wrapping
        var text = new TextViewNode
        {
            Font = _font,
            Text =
            [
                new TextNode { Text = "First" },
                new TextNode { Text = "Second" }
            ]
        };
        var root = new ViewNode { Children = { text } };

        // Very narrow — force "Second" onto a new line
        _engine.Layout(root, XUnit.FromPoint(60), XUnit.FromPoint(800));

        // "First" on line 0
        Assert.Equal(0, text.Text[0].Y.Point);
        // "Second" should be on a lower line (Y > 0)
        Assert.True(text.Text[1].Y.Point > 0,
            $"Wrapped word Y should be > 0 but was {text.Text[1].Y.Point}");
        // Wrapped word starts at X = 0
        Assert.Equal(0, text.Text[1].X.Point);
    }
}
