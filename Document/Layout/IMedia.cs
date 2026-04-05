using PdfSharp.Drawing;

namespace Document.Layout;

public struct TextMetrics
{
    public XUnit Width;
    public XUnit Height;
    public XUnit Baseline;
}

public interface IMedia
{
    TextMetrics MeasureString(string text, XFont font);
}
