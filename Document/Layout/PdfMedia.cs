using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Document.Layout;

public class PdfMedia : IMedia
{
    private readonly XGraphics _g;

    public PdfMedia()
    {
        var ctxSize = new XSize(2_000, 2_000);
        _g = XGraphics.CreateMeasureContext(ctxSize, XGraphicsUnit.Point, XPageDirection.Downwards);
    }

    public TextMetrics MeasureString(string text, XFont font)
    {
        // Guard against empty or whitespace-only strings that XGraphics returns 0 for
        if (string.IsNullOrEmpty(text))
        {
            // Return zero width but valid height, so line spacing is preserved
            var fallback = _g.MeasureString("X", font);
            return new TextMetrics
            {
                Width = XUnit.FromPoint(0),
                Height = XUnit.FromPoint(fallback.Height),
                Baseline = XUnit.FromPoint(fallback.Height / 2),
            };
        }

        var metrics = _g.MeasureString(text, font);
        return new TextMetrics
        {
            Height = XUnit.FromPoint(metrics.Height),
            Width = XUnit.FromPoint(metrics.Width),
            // todo: baseline
            Baseline = XUnit.FromPoint(metrics.Height / 2),
        };
    }
}