using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace ClarionTplDesigner;

/// <summary>Paints a soft band across each given document line — used to show the
/// lines of the currently selected control(s) in the source editor.</summary>
public class LineHighlighter : IBackgroundRenderer
{
    public readonly HashSet<int> Lines = new();        // 1-based document line numbers

    static readonly Brush Fill = new SolidColorBrush(Color.FromArgb(54, 230, 140, 40));
    static LineHighlighter() => Fill.Freeze();

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext dc)
    {
        if (Lines.Count == 0 || !textView.VisualLinesValid) return;
        foreach (var v in textView.VisualLines)
        {
            int ln = v.FirstDocumentLine.LineNumber;
            if (!Lines.Contains(ln)) continue;
            double y = v.VisualTop - textView.VerticalOffset;
            dc.DrawRectangle(Fill, null, new Rect(0, y, textView.ActualWidth, v.Height));
        }
    }
}
