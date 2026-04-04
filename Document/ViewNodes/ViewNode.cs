using System.Drawing;
using PdfSharp.Drawing;

namespace Document.ViewNodes;

public struct Bounds
{
    public XUnit X, Y, W, H;

    public Bounds()
    {
        X = XUnit.FromPoint(0);
        Y = XUnit.FromPoint(0);
        W = XUnit.FromPoint(0);
        H = XUnit.FromPoint(0);   
    }
}

public class ViewNode
{
    public ViewStyles Styles { get; set; } = new();
}

public struct TextNode
{
    public TextNode()
    {
    }

    public string Text { get; set; } = string.Empty;

    public XUnit Width { get; set; } = XUnit.FromPoint(0);
    public XUnit Height { get; set; } = XUnit.FromPoint(0);
}

public class TextViewNode : ViewNode
{
    public TextNode[] Text { get; init; } = [];
}

public class ImageViewNode : ViewNode
{
}