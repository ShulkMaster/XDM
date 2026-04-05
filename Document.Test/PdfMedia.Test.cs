using Document.Layout;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace Document.Test;

public class PdfMediaTest
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

    private readonly PdfMedia _media;
    private readonly XFont _font;

    public PdfMediaTest()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new TestFontResolver();

        _media = new PdfMedia();
        _font = new XFont("DejaVu Sans", 12);
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("World")]
    [InlineData("A")]
    [InlineData("test string")]
    [InlineData("M")]
    public void NormalStrings_ShouldHavePositiveWidthAndHeight(string text)
    {
        var metrics = _media.MeasureString(text, _font);

        Assert.True(metrics.Width.Point > 0, $"Width for '{text}' should be > 0 but was {metrics.Width.Point}");
        Assert.True(metrics.Height.Point > 0, $"Height for '{text}' should be > 0 but was {metrics.Height.Point}");
        Assert.True(metrics.Baseline.Point > 0, $"Baseline for '{text}' should be > 0 but was {metrics.Baseline.Point}");
    }

    [Fact]
    public void EmptyString_ShouldHaveZeroWidthButValidHeight()
    {
        var metrics = _media.MeasureString("", _font);

        Assert.Equal(0, metrics.Width.Point);
        Assert.True(metrics.Height.Point > 0, $"Height for empty string should be > 0 but was {metrics.Height.Point}");
        Assert.True(metrics.Baseline.Point > 0, $"Baseline for empty string should be > 0 but was {metrics.Baseline.Point}");
    }

    [Fact]
    public void Space_ShouldHavePositiveWidthAndHeight()
    {
        var metrics = _media.MeasureString(" ", _font);

        Assert.True(metrics.Width.Point > 0, $"Space width should be > 0 but was {metrics.Width.Point}");
        Assert.True(metrics.Height.Point > 0, $"Space height should be > 0 but was {metrics.Height.Point}");
    }

    [Fact]
    public void LongerString_ShouldBeWiderThanShorterString()
    {
        var short1 = _media.MeasureString("Hi", _font);
        var long1 = _media.MeasureString("Hello World", _font);

        Assert.True(long1.Width.Point > short1.Width.Point,
            $"'Hello World' ({long1.Width.Point}) should be wider than 'Hi' ({short1.Width.Point})");
    }

    [Fact]
    public void SameString_ShouldReturnConsistentMeasurements()
    {
        var first = _media.MeasureString("Test", _font);
        var second = _media.MeasureString("Test", _font);

        Assert.Equal(first.Width.Point, second.Width.Point);
        Assert.Equal(first.Height.Point, second.Height.Point);
        Assert.Equal(first.Baseline.Point, second.Baseline.Point);
    }
}
