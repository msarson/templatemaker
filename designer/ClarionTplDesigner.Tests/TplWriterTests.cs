using System.Linq;
using ClarionTplDesigner;
using Xunit;

namespace ClarionTplDesigner.Tests;

// Characterization tests for the template WRITER (TplWriter.PreviewFile / BuildLines / SECTION normalization
// / ApplyAt / ApplyPromptAt / ApplyProps). PreviewFile gives the text a Save WOULD write, without touching disk.
public class TplWriterTests
{
    static string Norm(string s) => s.Replace("\r\n", "\n").TrimEnd('\n');

    [Fact]
    public void PreviewFile_WithNoEdits_PreservesAnAlreadyNormalizedTemplateByteForByte()
    {
        // About is already SECTION and its child is positioned, so nothing needs rewriting.
        var src = """
            #EXTENSION(myX,'my X'),PROCEDURE
            #SHEET
              #TAB('&General')
                #BOXED('About'),SECTION,AT(5,5,250,40)
                  #DISPLAY('Hello'),AT(5,10,240,11)
                #ENDBOXED
              #ENDTAB
            #ENDSHEET
            """;
        var doc = TplParser.ParseText(src, "x.tpl");
        Assert.Equal(Norm(src), Norm(TplWriter.PreviewFile(doc, 0)));
    }

    [Fact]
    public void PreviewFile_DoesNotPromoteAPlainBoxToSection()
    {
        // SECTION is an explicit, optional attribute (per the Template Language reference): a plain #BOXED
        // keeps its child AT() on the window baseline. The designer honours that and NEVER injects SECTION —
        // Layout handles the tab-absolute-vs-box-relative difference, so the writer leaves a plain box alone.
        var src = """
            #EXTENSION(myX,'my X'),PROCEDURE
            #SHEET
              #TAB('&General')
                #BOXED('Options'),AT(5,5,250,40)
                  #PROMPT('&Disable',CHECK),%xDisable,DEFAULT(0),AT(6,10,88,11)
                #ENDBOXED
              #ENDTAB
            #ENDSHEET
            """;
        var doc = TplParser.ParseText(src, "x.tpl");
        var outText = TplWriter.PreviewFile(doc, 0);

        Assert.DoesNotContain("SECTION", outText);          // not promoted
        Assert.Equal(Norm(src), Norm(outText));             // and otherwise byte-preserved
    }

    [Fact]
    public void PreviewFile_EmitsPromptAtAndFontPropsForAnEditedPrompt()
    {
        var src = """
            #EXTENSION(f,'f'),PROCEDURE
            #SHEET
              #TAB('T')
                #PROMPT('&Name:',@s30),%xName,AT(96,10,120,11)
              #ENDTAB
            #ENDSHEET
            """;
        var doc = TplParser.ParseText(src, "x.tpl");
        var prompt = doc.Components.First(c => c.HasSheet).Tabs[0].Children.First(e => e.Symbol == "%xName");

        // Position the label (PROMPTAT) and set a bold Barlow face — the writer should emit both.
        prompt.HasPromptAt = prompt.HasPX = prompt.HasPY = true;
        prompt.PX = 5; prompt.PY = 10; prompt.Dirty = true;
        prompt.FontName = "Barlow"; prompt.FontStyle = 700; prompt.FontDirty = true;

        var outText = TplWriter.PreviewFile(doc, 0);
        Assert.Contains("PROMPTAT(5,10)", outText);
        Assert.Contains("PROP(PROP:FontName,'Barlow')", outText);
        Assert.Contains("PROP(PROP:FontStyle,700)", outText);
    }
}
