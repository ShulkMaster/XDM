using Document.ViewNodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Document;

public class PdfRenderer
{
    private readonly XFont _defaultFont = new XFont("Courier", 12);
    public void RenderPage(PdfPage page, ViewNode model)
    {
        using var gfx = XGraphics.FromPdfPage(page);
        RenderNode(gfx, model);
    }

    private void RenderNode(XGraphics gfx, ViewNode node)
    {
        if (node is TextViewNode textView)
        {
            RenderText(gfx, textView);
            return;
        }

        foreach (var child in node.Children)
        {
            RenderNode(gfx, child);
        }
    }

    private void RenderText(XGraphics gfx, TextViewNode textView)
    {
        if (textView.Text.Length == 0) return;

        var font = textView.Font ?? _defaultFont;
        var bounds = textView.Bounds;

        // Use the word positions (X, Y) already computed by the LayoutEngine
        for (var i = 0; i < textView.Text.Length; i++)
        {
            ref var word = ref textView.Text[i];
            if (!word.Measured) continue;
            if (word.Text == "\n") continue;

            var drawX = bounds.X + word.X;
            var drawY = bounds.Y + word.Y;
            
            Console.WriteLine($"Drawing word '{word.Text}' at ({drawX.Point}, {drawY.Point})");

            gfx.DrawString(word.Text, font, XBrushes.Black, drawX.Point, drawY.Point + word.Baseline.Point);
        }
    }
}
