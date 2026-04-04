using System.Drawing;
using PdfSharp.Drawing;

namespace Document.ViewNodes;

public enum SizeUnit
{
    Ppi,
    Percent,
    Viewport,
}

public enum SizeMode
{
    Auto,
    Fixed,
}

public sealed class Padding
{
    public int Top { get; set; }
    public int Bottom { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
}

public sealed class Margin
{
    public int Top { get; set; }
    public int Bottom { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
}

public sealed class Background
{
    public Color Color { get; set; } = Color.Transparent;
    // todo: image
    // todo: gradient
}



public class ViewStyles
{
    public Padding Padding { get; set; } = new();
    public Margin Margin { get; set; } = new();
    public XUnit Width { get; set; } = XUnit.FromPoint(0);
    public XUnit Height { get; set; } = XUnit.FromPoint(0);
    public SizeMode WidthMode { get; set; } = SizeMode.Auto;
    public SizeMode HeightMode { get; set; } = SizeMode.Auto;
    
    public Background Background { get; set; } = new();
}