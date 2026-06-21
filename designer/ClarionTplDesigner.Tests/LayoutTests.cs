using ClarionTplDesigner;
using Xunit;

namespace ClarionTplDesigner.Tests;

// Characterization tests for Layout (the design-canvas math): auto-flow stacking, side-label column
// reservation, #BOXED,SECTION coordinate rebasing, #IMAGE footprint reservation, and the label rect.
public class LayoutTests
{
    // ---- builders -------------------------------------------------------
    static TplElement Tab(params TplElement[] kids)
    {
        var t = new TplElement { Kind = TplKind.Tab, Title = "T" };
        foreach (var k in kids) { k.Parent = t; t.Children.Add(k); }
        return t;
    }
    static TplElement El(TplKind kind, string type = "", string title = "",
                         int? x = null, int? y = null, int? w = null, int? h = null, bool section = false)
    {
        var e = new TplElement { Kind = kind, PromptType = type, Title = title, Section = section };
        if (x.HasValue) { e.HasX = true; e.X = x.Value; }
        if (y.HasValue) { e.HasY = true; e.Y = y.Value; }
        if (w.HasValue) { e.HasW = true; e.W = w.Value; }
        if (h.HasValue) { e.HasH = true; e.H = h.Value; }
        return e;
    }
    static TplElement Prompt(string type, string title) => El(TplKind.Prompt, type, title);
    static TplElement Box(string title, bool section, int x, int y, int w, int h, params TplElement[] kids)
    {
        var b = El(TplKind.Boxed, title: title, x: x, y: y, w: w, h: h, section: section);
        foreach (var k in kids) { k.Parent = b; b.Children.Add(k); }
        return b;
    }

    // ---- tests ----------------------------------------------------------

    [Fact]
    public void AutoFlow_StacksControlsTopToBottomAtTheIndent()
    {
        var a = El(TplKind.Display); var b = El(TplKind.Display); var c = El(TplKind.Display);
        Layout.Run(Tab(a, b, c));

        Assert.Equal(6, a.LX, 3); Assert.Equal(6, b.LX, 3); Assert.Equal(6, c.LX, 3);
        Assert.Equal(2, a.LY, 3);          // tab top
        Assert.Equal(16, b.LY, 3);         // + 11 (height) + 3 (gap)
        Assert.Equal(30, c.LY, 3);
        Assert.True(b.LY > a.LY && c.LY > b.LY);
    }

    [Fact]
    public void AutoFlow_SideLabelPrompt_ReservesTheLabelColumn()
    {
        var entry = Prompt("@s255", "Name:");   // side label
        var check = Prompt("CHECK", "Enabled");  // inline caption — no side label
        Layout.Run(Tab(entry, check));

        // entry indents past the reserved label column (Indent 6 + EstLabelW("Name:")=30); check sits at Indent.
        Assert.Equal(6 + Layout.EstLabelW(entry), entry.LX, 3);
        Assert.Equal(6, check.LX, 3);
        Assert.True(entry.LX > check.LX);
    }

    [Fact]
    public void HasSideLabel_TrueForEntryTypes_FalseForCheckOptionRadioAndNonPrompts()
    {
        Assert.True(Layout.HasSideLabel(Prompt("@s255", "x")));
        Assert.True(Layout.HasSideLabel(Prompt("COLOR", "x")));
        Assert.False(Layout.HasSideLabel(Prompt("CHECK", "x")));
        Assert.False(Layout.HasSideLabel(Prompt("OPTION", "x")));
        Assert.False(Layout.HasSideLabel(Prompt("RADIO", "x")));
        Assert.False(Layout.HasSideLabel(El(TplKind.Display, title: "x")));
    }

    [Fact]
    public void SectionBox_RebasesChildAtToTheBoxOrigin()
    {
        var child = El(TplKind.Prompt, "@s255", "C", x: 6, y: 10);
        var box = Box("Opt", section: true, x: 5, y: 50, w: 250, h: 96, child);
        Layout.Run(Tab(box));

        // SECTION → child AT(6,10) resolves against the box origin (5,50) → (11,60).
        Assert.Equal(11, child.LX, 3);
        Assert.Equal(60, child.LY, 3);
    }

    [Fact]
    public void PlainBox_DoesNotRebaseChildAt_StaysTabAbsolute()
    {
        var child = El(TplKind.Prompt, "@s255", "C", x: 6, y: 10);
        var box = Box("Opt", section: false, x: 5, y: 50, w: 250, h: 96, child);
        Layout.Run(Tab(box));

        // No SECTION → child AT(6,10) is tab-absolute, unaffected by the box position.
        Assert.Equal(6, child.LX, 3);
        Assert.Equal(10, child.LY, 3);
    }

    [Fact]
    public void Image_ReservesItsRealFootprint_SoFollowingControlsFlowBelow()
    {
        var prev = Layout.ImageIntrinsic;
        Layout.ImageIntrinsic = _ => (100.0, 80.0);
        try
        {
            var img = El(TplKind.Image, title: "logo.png");
            var after = El(TplKind.Display);
            Layout.Run(Tab(img, after));

            Assert.Equal(100, img.LW, 3);
            Assert.Equal(80, img.LH, 3);
            Assert.True(after.LY >= img.LY + img.LH);   // flows below the image, not under it
        }
        finally { Layout.ImageIntrinsic = prev; }
    }

    [Fact]
    public void PromptLabelRect_DefaultsToLeftOfTheEntry()
    {
        var entry = Prompt("@s255", "Name:");
        Layout.Run(Tab(entry));

        // no PROMPTAT → label sits immediately left of the entry: PLX = LX - PLW, same row.
        Assert.Equal(Layout.EstLabelW(entry), entry.PLW, 3);
        Assert.Equal(entry.LX - entry.PLW, entry.PLX, 3);
        Assert.Equal(entry.LY, entry.PLY, 3);
    }

    [Fact]
    public void PromptLabelRect_HonoursExplicitPromptAt()
    {
        var entry = El(TplKind.Prompt, "@s255", "Name:", x: 101, y: 20);
        entry.HasPromptAt = entry.HasPX = entry.HasPY = true;
        entry.PX = 5; entry.PY = 20;
        Layout.Run(Tab(entry));

        Assert.Equal(5, entry.PLX, 3);    // PROMPTAT x, tab origin = 0
        Assert.Equal(20, entry.PLY, 3);   // PROMPTAT y
    }
}
