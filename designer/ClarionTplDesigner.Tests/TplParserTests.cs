using System.Linq;
using ClarionTplDesigner;
using Xunit;

namespace ClarionTplDesigner.Tests;

// Characterization tests for the template PARSER (TplParser / NewEl / ParseAt / ParsePromptAt / ParseProps).
public class TplParserTests
{
    // A small but representative extension prompt sheet used across the parser tests.
    const string Sample = """
        #TEMPLATE(t,'t'),FAMILY('ABC')
        #EXTENSION(myX,'my X'),PROCEDURE
        #SHEET
          #TAB('&General')
            #BOXED('About'),SECTION
              #DISPLAY('Hello')
            #ENDBOXED
            #BOXED('Options')
              #PROMPT('&Disable',CHECK),%xDisable,DEFAULT(0),AT(10)
              #PROMPT('&Color:',COLOR),%xColor,DEFAULT(-1),PROMPTAT(5,20),AT(101,20,139,11)
            #ENDBOXED
          #ENDTAB
        #ENDSHEET
        """;

    static TplComponent ParseSheet(string text) =>
        TplParser.ParseText(text, "x.tpl").Components.First(c => c.HasSheet);

    [Fact]
    public void ParseText_FindsTheSheetComponentTabsAndControls()
    {
        var comp = ParseSheet(Sample);
        Assert.Equal("EXTENSION", comp.Kind);
        Assert.Equal("myX", comp.Name);
        Assert.Single(comp.Tabs);
        Assert.Equal("&General", comp.Tabs[0].Title);

        var boxes = comp.Tabs[0].Children.Where(e => e.Kind == TplKind.Boxed).ToList();
        Assert.Equal(2, boxes.Count);
        Assert.Equal("About", boxes[0].Title);
        Assert.Equal("Options", boxes[1].Title);
    }

    [Fact]
    public void ParseSection_DetectsBoxedSectionAttribute()
    {
        var boxes = ParseSheet(Sample).Tabs[0].Children.Where(e => e.Kind == TplKind.Boxed).ToList();
        Assert.True(boxes[0].Section);    // #BOXED('About'),SECTION
        Assert.False(boxes[1].Section);   // #BOXED('Options')  — no SECTION
    }

    [Fact]
    public void ParseAt_ReadsSlotsAndHasFlags()
    {
        var options = ParseSheet(Sample).Tabs[0].Children.First(e => e.Title == "Options");
        var disable = options.Children.First(e => e.Symbol == "%xDisable");
        var color = options.Children.First(e => e.Symbol == "%xColor");

        // AT(10) — X only
        Assert.True(disable.HasX); Assert.Equal(10, disable.X);
        Assert.False(disable.HasY); Assert.False(disable.HasW);

        // AT(101,20,139,11) — all four
        Assert.True(color.HasX && color.HasY && color.HasW && color.HasH);
        Assert.Equal(101, color.X); Assert.Equal(20, color.Y);
        Assert.Equal(139, color.W); Assert.Equal(11, color.H);
    }

    [Fact]
    public void ParsePromptAt_ReadsLabelPositionIndependentlyOfAt()
    {
        var color = ParseSheet(Sample).Tabs[0].Children
            .First(e => e.Title == "Options").Children.First(e => e.Symbol == "%xColor");
        Assert.True(color.HasPromptAt);
        Assert.True(color.HasPX && color.HasPY);
        Assert.Equal(5, color.PX);
        Assert.Equal(20, color.PY);
    }

    [Fact]
    public void ParsePromptType_AndDefault_AreCaptured()
    {
        var color = ParseSheet(Sample).Tabs[0].Children
            .First(e => e.Title == "Options").Children.First(e => e.Symbol == "%xColor");
        Assert.Equal("COLOR", color.PromptType);
        Assert.Equal("-1", color.DefaultExpr);
    }

    [Theory]
    [InlineData(400, false, false, false)]   // regular
    [InlineData(700, true, false, false)]    // bold
    [InlineData(4496, false, true, false)]   // 400 + italic (0x1000) — NOT bold (weight is 400)
    [InlineData(4796, true, true, false)]    // 700 + italic
    [InlineData(8592, false, false, true)]   // 400 + underline (0x2000)
    public void ParseFontStyle_DerivesBoldItalicUnderlineFromTheBits(int style, bool bold, bool italic, bool underline)
    {
        var e = ParseSheet($"""
            #EXTENSION(f,'f'),PROCEDURE
            #SHEET
              #TAB('T')
                #DISPLAY('x'),PROP(PROP:FontStyle,{style})
              #ENDTAB
            #ENDSHEET
            """).Tabs[0].Children.First(c => c.Kind == TplKind.Display);

        Assert.Equal(style, e.FontStyle);
        Assert.Equal(bold, e.Bold);
        Assert.Equal(italic, e.Italic);
        Assert.Equal(underline, e.Underline);
    }

    [Theory]
    [InlineData("PROP(PROP:FontName,'Barlow')")]   // correct property
    [InlineData("PROP(PROP:Font,'Barlow')")]        // legacy form — still read
    public void ParseFontName_AcceptsBothPropFontNameAndLegacyPropFont(string prop)
    {
        var e = ParseSheet($"""
            #EXTENSION(f,'f'),PROCEDURE
            #SHEET
              #TAB('T')
                #DISPLAY('x'),{prop}
              #ENDTAB
            #ENDSHEET
            """).Tabs[0].Children.First(c => c.Kind == TplKind.Display);
        Assert.Equal("Barlow", e.FontName);
    }
}
