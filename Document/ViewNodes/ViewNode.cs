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
    public Bounds Bounds { get; set; } = new();
    public List<ViewNode> Children { get; init; } = [];
}

public struct TextNode
{
    public TextNode()
    {
    }

    public string Text { get; set; } = string.Empty;

    // Memoized measurements per word
    public XUnit Width { get; set; } = XUnit.FromPoint(0);
    public XUnit Height { get; set; } = XUnit.FromPoint(0);
    public XUnit Baseline { get; set; } = XUnit.FromPoint(0);
    public bool Measured { get; set; } = false;

    // Layout-computed position (relative to the TextViewNode)
    public XUnit X { get; set; } = XUnit.FromPoint(0);
    public XUnit Y { get; set; } = XUnit.FromPoint(0);
}

public class TextViewNode : ViewNode
{
    public TextNode[] Text { get; init; } = [];
    public XFont? Font { get; set; }
}

public class ImageViewNode : ViewNode
{
}
