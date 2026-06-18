using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ClarionTplDesigner;

/// <summary>A code overview strip: each source line is a coloured bar (comment / directive / other),
/// scaled to fit, with a viewport box. Click or drag to jump the editor.</summary>
public class MinimapControl : FrameworkElement
{
    public string[] Lines = Array.Empty<string>();
    public int FirstVisible, VisibleCount;        // current editor viewport, in lines
    public event Action<int>? GoToLine;

    static readonly Brush Bg        = new SolidColorBrush(Color.FromRgb(244, 246, 249));
    static readonly Brush Comment   = new SolidColorBrush(Color.FromRgb(120, 175, 120));
    static readonly Brush Directive = new SolidColorBrush(Color.FromRgb(90, 140, 200));
    static readonly Brush Symbol    = new SolidColorBrush(Color.FromRgb(70, 160, 150));
    static readonly Brush Plain     = new SolidColorBrush(Color.FromRgb(170, 178, 190));
    static readonly Brush Viewport  = new SolidColorBrush(Color.FromArgb(40, 0, 120, 200));
    static readonly Pen   VpPen     = new(new SolidColorBrush(Color.FromArgb(150, 0, 120, 200)), 1);

    static MinimapControl()
    {
        Bg.Freeze(); Comment.Freeze(); Directive.Freeze(); Symbol.Freeze(); Plain.Freeze();
        Viewport.Freeze(); VpPen.Freeze();
    }

    public MinimapControl() { ClipToBounds = true; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Bg, null, new Rect(0, 0, w, h));
        int n = Lines.Length;
        if (n == 0) return;

        double rowH = h / n;
        double barH = Math.Max(0.7, rowH - 0.3);
        for (int i = 0; i < n; i++)
        {
            string raw = Lines[i];
            string t = raw.TrimStart();
            if (t.Length == 0) continue;
            Brush b = t.StartsWith("#!") || t.StartsWith("!") ? Comment
                    : t.StartsWith("#") ? Directive
                    : t.Contains('%') ? Symbol : Plain;
            int indent = raw.Length - t.Length;
            double x = 2 + Math.Min(indent * 0.5, w / 3);
            int len = Math.Min(raw.TrimEnd().Length, 90);
            double bw = Math.Max(1, (w - x - 2) * (len / 90.0));
            dc.DrawRectangle(b, null, new Rect(x, i * rowH, bw, barH));
        }

        if (VisibleCount > 0)
        {
            double vy = (double)FirstVisible / n * h;
            double vh = Math.Max(6, (double)VisibleCount / n * h);
            dc.DrawRectangle(Viewport, VpPen, new Rect(0.5, vy, w - 1, vh));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { CaptureMouse(); Jump(e.GetPosition(this)); }
    protected override void OnMouseMove(MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) Jump(e.GetPosition(this)); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => ReleaseMouseCapture();

    void Jump(Point p)
    {
        int n = Lines.Length;
        if (n == 0) return;
        int line = (int)(p.Y / Math.Max(1, ActualHeight) * n);
        GoToLine?.Invoke(Math.Max(0, Math.Min(n - 1, line)));
    }
}
