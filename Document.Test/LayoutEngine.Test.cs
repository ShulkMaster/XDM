using Document.Layout;
using Document.ViewNodes;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using Xunit;

namespace Document.Test;

public class LayoutEngineTest
{
    private class TestFontResolver : IFontResolver
    {
        public byte[] GetFont(string faceName)
        {
            var path = $"/usr/share/fonts/TTF/{faceName}.ttf";
            if (File.Exists(path))
                return File.ReadAllBytes(path);

            // Fallback for names like "Arial" to something we have
            return File.ReadAllBytes("/usr/share/fonts/TTF/DejaVuSans.ttf");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            return new FontResolverInfo("DejaVuSans");
        }
    }

    private class MockMedia : IMedia
    {
        public TextMetrics MeasureString(string text, XFont font)
        {
            // Simple: 10 units per character, height same as font size
            return new TextMetrics
            {
                Width = XUnit.FromPoint(text.Length * 10),
                Height = XUnit.FromPoint(font.Size),
                Baseline = XUnit.FromPoint(font.Size * 0.8)
            };
        }
    }

    private readonly LayoutEngine _engine;
    private readonly XFont _font;

    public LayoutEngineTest()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new TestFontResolver();
        
        _engine = new LayoutEngine(new MockMedia());
        _font = new XFont("DejaVu Sans", 10);
    }

    [Fact]
    public void SingleLineTest()
    {
        // Multiple words on a single line, verify X positions advance correctly
        // MockMedia: each char = 10pt wide, space = font.Size * 0.3 = 3pt
        var text = new TextViewNode
        {
            Font = _font,
            Text = [
                new TextNode { Text = "Hello" },   // 5*10 = 50
                new TextNode { Text = "World" },   // 5*10 = 50
                new TextNode { Text = "Foo" },     // 3*10 = 30
                new TextNode { Text = "Bar" }      // 3*10 = 30
            ]
        };
        var root = new ViewNode { Children = { text } };

        // Container wide enough to fit all: 50 + 3 + 50 + 3 + 30 + 3 + 30 = 169
        _engine.Layout(root, XUnit.FromPoint(200), XUnit.FromPoint(100));

        // Text should use full available width (maxWidth)
        Assert.Equal(200, text.Bounds.W.Point);
        // Single line height
        Assert.Equal(10, text.Bounds.H.Point);

        // Verify word X positions advance along the line
        // "Hello" starts at X=0
        Assert.Equal(0, text.Text[0].X.Point);
        // "World" starts at X = 50 (Hello) + 3 (space) = 53
        Assert.Equal(53, text.Text[1].X.Point);
        // "Foo" starts at X = 50 + 3 + 50 + 3 = 106
        Assert.Equal(106, text.Text[2].X.Point);
        // "Bar" starts at X = 50 + 3 + 50 + 3 + 30 + 3 = 139
        Assert.Equal(139, text.Text[3].X.Point);
    }

    [Fact]
    public void WrapAroundTest()
    {
        // Two words that should wrap if container is small
        var text = new TextViewNode
        {
            Font = _font,
            Text = [
                new TextNode { Text = "First" },  // 50
                new TextNode { Text = "Second" }   // 60
            ]
        };
        var root = new ViewNode { Children = { text } };

        // Max width 80. First (50) + Space (3) + Second (60) = 113 > 80.
        // Should wrap to two lines.
        _engine.Layout(root, XUnit.FromPoint(80), XUnit.FromPoint(200));

        // Text should be as wide as possible (maxWidth = 80)
        Assert.Equal(80, text.Bounds.W.Point);
        Assert.Equal(20, text.Bounds.H.Point); // 2 lines * 10
    }

    [Fact]
    public void LineBreakTest()
    {
        var text = new TextViewNode
        {
            Font = _font,
            Text = [
                new TextNode { Text = "Line1" },
                new TextNode { Text = "\n" },
                new TextNode { Text = "Line2" }
            ]
        };
        var root = new ViewNode { Children = { text } };

        _engine.Layout(root, XUnit.FromPoint(200), XUnit.FromPoint(200));

        // Explicit \n forces two lines
        Assert.Equal(20, text.Bounds.H.Point);
        // Text width is maxWidth
        Assert.Equal(200, text.Bounds.W.Point);
    }
}
