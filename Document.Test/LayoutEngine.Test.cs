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
        var text = new TextViewNode
        {
            Font = _font,
            Text = [new TextNode { Text = "Hello" }]
        };
        var root = new ViewNode { Children = { text } };

        _engine.Layout(root, XUnit.FromPoint(100), XUnit.FromPoint(100));

        // Hello = 5 * 10 = 50 width
        Assert.Equal(50, text.Bounds.W.Point);
        Assert.Equal(10, text.Bounds.H.Point);
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
                new TextNode { Text = "Second" } // 60
            ]
        };
        var root = new ViewNode { Children = { text } };

        // Max width 80. First (50) + Space (10 * 0.3 = 3) + Second (60) = 113 > 80.
        // Should wrap to two lines.
        _engine.Layout(root, XUnit.FromPoint(80), XUnit.FromPoint(200));

        // Max width encountered was "Second" (60) because it wrapped.
        // But the requirement says "wraps around because of the container max width was reached"
        // In my current implementation, the width of the TextView becomes the width of the widest line.
        Assert.Equal(60, text.Bounds.W.Point);
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

        // We'll see how LayoutEngine handles this. 
        // Current implementation: only wraps if cursorX + advance > maxWidth.
        // It doesn't seem to check for "\n" character.
        // If it doesn't wrap, H will be 10. If it wraps, H will be 20.
        Assert.Equal(20, text.Bounds.H.Point);
    }
}