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
        var maxWidth = bounds.W;
        var spaceWidth = XUnit.FromPoint(font.Size * 0.3);

        var cursorX = XUnit.FromPoint(0);
        var lineMaxAscent = XUnit.FromPoint(0);
        var lineMaxDescent = XUnit.FromPoint(0);
        var totalHeight = XUnit.FromPoint(0);
        var isFirstWordOnLine = true;

        // Collect words for the current line so we can draw them with correct baseline
        var lineWords = new List<(int index, XUnit x)>();

        for (var i = 0; i < textView.Text.Length; i++)
        {
            ref var word = ref textView.Text[i];
            if (!word.Measured) continue;

            var wordWidth = word.Width;
            var advance = isFirstWordOnLine ? wordWidth : spaceWidth + wordWidth;

            // Check if word wraps to next line
            if (!isFirstWordOnLine && cursorX.Point + advance.Point > maxWidth.Point)
            {
                // Draw the completed line
                DrawLine(gfx, textView, lineWords, bounds.X, bounds.Y + totalHeight, lineMaxAscent, font);

                // Advance to next line
                var lineHeight = lineMaxAscent + lineMaxDescent;
                totalHeight += lineHeight;

                cursorX = XUnit.FromPoint(0);
                lineMaxAscent = XUnit.FromPoint(0);
                lineMaxDescent = XUnit.FromPoint(0);
                isFirstWordOnLine = true;
                advance = wordWidth;
                lineWords.Clear();
            }

            var wordX = isFirstWordOnLine ? XUnit.FromPoint(0) : cursorX + spaceWidth;
            if (isFirstWordOnLine) wordX = cursorX;

            lineWords.Add((i, wordX));

            var ascent = word.Baseline;
            var descent = XUnit.FromPoint(word.Height.Point - word.Baseline.Point);

            if (ascent.Point > lineMaxAscent.Point)
                lineMaxAscent = ascent;
            if (descent.Point > lineMaxDescent.Point)
                lineMaxDescent = descent;

            cursorX += advance;
            isFirstWordOnLine = false;
        }

        // Draw the last line
        if (lineWords.Count > 0)
        {
            DrawLine(gfx, textView, lineWords, bounds.X, bounds.Y + totalHeight, lineMaxAscent, font);
        }
    }

    private static void DrawLine(
        XGraphics gfx,
        TextViewNode textView,
        List<(int index, XUnit x)> lineWords,
        XUnit originX,
        XUnit originY,
        XUnit lineMaxAscent,
        XFont font)
    {
        foreach (var (index, wordX) in lineWords)
        {
            ref var word = ref textView.Text[index];

            // Align on baseline: offset = lineMaxAscent - word.Baseline
            var baselineOffset = lineMaxAscent - word.Baseline;
            var drawX = originX + wordX;
            var drawY = originY + baselineOffset;

            gfx.DrawString(word.Text, font, XBrushes.Black, drawX.Point, drawY.Point + word.Baseline.Point);
        }
    }
}
