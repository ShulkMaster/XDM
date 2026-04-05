using Document.ViewNodes;
using PdfSharp.Drawing;

namespace Document.Layout;

/// <summary>
/// Simple layout engine inspired by the Clay C layout library.
/// Phases: MeasureText -> SizeContainersAlongAxis -> FinalizeLayout
/// </summary>
public class LayoutEngine
{
    private readonly IMedia _media;
    private readonly XFont _defaultFont = new XFont("Courier", 12);

    public LayoutEngine(IMedia media)
    {
        _media = media;
    }

    /// <summary>
    /// Run the full layout pass on the tree with the given container dimensions.
    /// </summary>
    public void Layout(ViewNode root, XUnit containerWidth, XUnit containerHeight)
    {
        // Phase 1: Measure all text words and memoize
        MeasureTextNodes(root);

        // Phase 2: Size text views (Fixed) and lay out text with wrapping/baseline
        SizeTextViews(root, containerWidth);

        // Phase 3: Propagate fixed sizes upward (Auto containers become Fixed when all children are Fixed)
        PropagateFixedSizes(root);

        // Phase 4: Finalize positions
        FinalizeLayout(root, XUnit.FromPoint(0), XUnit.FromPoint(0), containerWidth, containerHeight);
    }

    // ── Phase 1: Measure text word-by-word ──────────────

    private void MeasureTextNodes(ViewNode node)
    {
        if (node is TextViewNode textView)
        {
            var font = textView.Font ?? _defaultFont;

            for (var i = 0; i < textView.Text.Length; i++)
            {
                ref var tn = ref textView.Text[i];
                var metrics = _media.MeasureString(tn.Text, font);

                tn.Width = metrics.Width;
                tn.Height = metrics.Height;
                tn.Baseline = metrics.Baseline;
                tn.Measured = true;

                if (metrics.Width < 0 && metrics.Height < 0)
                {
                    throw new Exception($"Invalid font metrics {tn.Text}");
                }
            }
        }

        foreach (var child in node.Children)
        {
            MeasureTextNodes(child);
        }
    }

    // ── Phase 2: Size text views with word wrapping and baseline alignment ──

    private void SizeTextViews(ViewNode node, XUnit availableWidth)
    {
        var padLeft = XUnit.FromPoint(node.Styles.Padding.Start);
        var padRight = XUnit.FromPoint(node.Styles.Padding.End);
        var padTop = XUnit.FromPoint(node.Styles.Padding.Top);
        var padBottom = XUnit.FromPoint(node.Styles.Padding.Bottom);

        // If the container has a Fixed width, that becomes the new available width
        var effectiveWidth = node.Styles.WidthMode == SizeMode.Fixed
            ? node.Styles.Width
            : availableWidth;

        var innerWidth = effectiveWidth - padLeft - padRight;
        if (innerWidth.Point < 0) innerWidth = XUnit.FromPoint(0);

        if (node is TextViewNode textView)
        {
            textView.Font ??= _defaultFont;
            LayoutTextContent(textView, innerWidth);
            return;
        }

        foreach (var child in node.Children)
        {
            SizeTextViews(child, innerWidth);
        }
    }

    /// <summary>
    /// Lay out text words with wrapping. Aligns text on baseline so mixed font sizes
    /// in the same line align as in a browser. Tracks highest and lowest points per line
    /// to compute correct Y offset for the next line.
    /// </summary>
    private static void LayoutTextContent(TextViewNode textView, XUnit maxWidth)
    {
        if (textView.Text.Length == 0) return;

        var font = textView.Font!;
        var spaceWidth = XUnit.FromPoint(0);

        // Estimate space width from first word metrics (proportional to font size)
        // We'll use a fraction of the em size
        if (textView.Text.Length > 0 && textView.Text[0].Measured)
        {
            // Approximate space as ~0.3 of font em size
            spaceWidth = XUnit.FromPoint(font.Size * 0.3);
        }

        var cursorX = XUnit.FromPoint(0);
        var maxCursorX = XUnit.FromPoint(0);
        // Per-line tracking: highest ascent (baseline from top) and total height
        var lineMaxAscent = XUnit.FromPoint(0); // max baseline in current line
        var lineMaxDescent = XUnit.FromPoint(0); // max (height - baseline) in current line
        var totalHeight = XUnit.FromPoint(0);
        var isFirstWordOnLine = true;

        for (var i = 0; i < textView.Text.Length; i++)
        {
            ref var word = ref textView.Text[i];
            if (!word.Measured) continue;

            var wordWidth = word.Width;
            var advance = isFirstWordOnLine ? wordWidth : spaceWidth + wordWidth;

            // Check if word is an explicit line break OR wraps to next line
            var isExplicitLineBreak = word.Text == "\n";
            if (isExplicitLineBreak || (!isFirstWordOnLine && cursorX.Point + advance.Point > maxWidth.Point))
            {
                // Update max width before line break
                if (cursorX.Point > maxCursorX.Point)
                    maxCursorX = cursorX;

                // Finish current line: advance totalHeight by line height
                var lineHeight = lineMaxAscent + lineMaxDescent;

                // If the line was empty (e.g. leading \n), we still want the height of a space/default
                if (lineHeight.Point == 0)
                    lineHeight = XUnit.FromPoint(font.Size);

                totalHeight += lineHeight;

                // Start new line
                cursorX = XUnit.FromPoint(0);
                lineMaxAscent = XUnit.FromPoint(0);
                lineMaxDescent = XUnit.FromPoint(0);
                isFirstWordOnLine = true;
                advance = wordWidth;

                if (isExplicitLineBreak) continue;
            }

            // Update line metrics for baseline alignment
            var ascent = word.Baseline;
            var descent = XUnit.FromPoint(word.Height.Point - word.Baseline.Point);

            if (ascent.Point > lineMaxAscent.Point)
                lineMaxAscent = ascent;
            if (descent.Point > lineMaxDescent.Point)
                lineMaxDescent = descent;

            // Store word position (relative to the TextViewNode)
            word.X = isFirstWordOnLine ? cursorX : cursorX + spaceWidth;
            word.Y = totalHeight;

            cursorX += advance;
            isFirstWordOnLine = false;
        }

        // Account for the last line
        if (cursorX.Point > maxCursorX.Point)
            maxCursorX = cursorX;

        var lastLineHeight = lineMaxAscent + lineMaxDescent;
        totalHeight += lastLineHeight;

        // Text should be as wide as possible (use maxWidth), only wrapping when exceeded
        var bounds = textView.Bounds;
        bounds.W = maxWidth;
        bounds.H = totalHeight;
        textView.Bounds = bounds;

        var styles = textView.Styles;
        styles.Width = maxWidth;
        styles.Height = totalHeight;
        styles.WidthMode = SizeMode.Fixed;
        styles.HeightMode = SizeMode.Fixed;
        textView.Styles = styles;
    }

    // ── Phase 3: Propagate Fixed sizes upward ────────────────────────────

    /// <summary>
    /// If all children of an Auto container have Fixed sizes, compute the
    /// container's size from its children and mark it Fixed.
    /// </summary>
    private static void PropagateFixedSizes(ViewNode node)
    {
        // Post-order: process children first
        foreach (var child in node.Children)
        {
            PropagateFixedSizes(child);
        }

        if (node.Children.Count == 0) return;

        // Check width: if all children are Fixed width, container gets Fixed width
        if (node.Styles.WidthMode == SizeMode.Auto)
        {
            var allFixedW = true;
            var maxW = XUnit.FromPoint(0);
            foreach (var child in node.Children)
            {
                if (child.Styles.WidthMode != SizeMode.Fixed)
                {
                    allFixedW = false;
                    break;
                }

                var childTotalW = child.Styles.Width
                                  + XUnit.FromPoint(child.Styles.Margin.Start)
                                  + XUnit.FromPoint(child.Styles.Margin.End);
                if (childTotalW.Point > maxW.Point)
                    maxW = childTotalW;
            }

            if (allFixedW)
            {
                var padH = XUnit.FromPoint(node.Styles.Padding.Start + node.Styles.Padding.End);
                node.Styles.WidthMode = SizeMode.Fixed;
                node.Styles.Width = maxW + padH;
            }
        }

        // Check height: if all children are Fixed height, container gets Fixed height
        if (node.Styles.HeightMode == SizeMode.Auto)
        {
            var allFixedH = true;
            var sumH = XUnit.FromPoint(0);
            foreach (var child in node.Children)
            {
                if (child.Styles.HeightMode != SizeMode.Fixed)
                {
                    allFixedH = false;
                    break;
                }

                sumH += child.Styles.Height
                        + XUnit.FromPoint(child.Styles.Margin.Top)
                        + XUnit.FromPoint(child.Styles.Margin.Bottom);
            }

            if (allFixedH)
            {
                var padV = XUnit.FromPoint(node.Styles.Padding.Top + node.Styles.Padding.Bottom);
                node.Styles.HeightMode = SizeMode.Fixed;
                node.Styles.Height = sumH + padV;
            }
        }
    }

    // ── Phase 4: Finalize positions ──────────────────────────────────────

    private static void FinalizeLayout(ViewNode node, XUnit x, XUnit y, XUnit availW, XUnit availH)
    {
        var padLeft = XUnit.FromPoint(node.Styles.Padding.Start);
        var padRight = XUnit.FromPoint(node.Styles.Padding.End);
        var padTop = XUnit.FromPoint(node.Styles.Padding.Top);
        var padBottom = XUnit.FromPoint(node.Styles.Padding.Bottom);

        var marginLeft = XUnit.FromPoint(node.Styles.Margin.Start);
        var marginRight = XUnit.FromPoint(node.Styles.Margin.End);
        var marginTop = XUnit.FromPoint(node.Styles.Margin.Top);
        var marginBottom = XUnit.FromPoint(node.Styles.Margin.Bottom);

        var nodeX = x + marginLeft;
        var nodeY = y + marginTop;

        var nodeW = node.Styles.WidthMode == SizeMode.Fixed
            ? node.Styles.Width
            : availW - marginLeft - marginRight;

        var nodeH = node.Styles.HeightMode == SizeMode.Fixed
            ? node.Styles.Height
            : availH - marginTop - marginBottom;

        var bounds = node.Bounds;
        bounds.X = nodeX;
        bounds.Y = nodeY;
        bounds.W = nodeW;
        bounds.H = nodeH;
        node.Bounds = bounds;

        // Layout children top-to-bottom
        var innerX = nodeX + padLeft;
        var innerY = nodeY + padTop;
        var innerW = nodeW - padLeft - padRight;
        var innerH = nodeH - padTop - padBottom;

        var childY = innerY;
        foreach (var child in node.Children)
        {
            var childMarginTop = XUnit.FromPoint(child.Styles.Margin.Top);
            var childMarginBottom = XUnit.FromPoint(child.Styles.Margin.Bottom);

            var childH = child.Styles.HeightMode == SizeMode.Fixed
                ? child.Styles.Height
                : XUnit.FromPoint(0);

            FinalizeLayout(child, innerX, childY, innerW, childH + childMarginTop + childMarginBottom);

            childY += child.Bounds.H + childMarginTop + childMarginBottom;
        }
    }
}