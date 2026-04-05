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