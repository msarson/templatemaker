using System;
using System.Collections.Generic;
using QrCodeCore;
using ZXing;
using ZXing.Common;
using Xunit;

namespace QrCodeCore.Tests;

// Tests for the QR encoder. The decisive ones DECODE the encoder's own matrix with an independent library
// (ZXing.Net) and assert the text round-trips — i.e. the code is genuinely scannable. Plus structural checks.
public class QrEncoderTests
{
    // ---- decode helper: render the matrix to a luminance image and decode it with ZXing -----------------
    static string? Decode(bool[,] m)
    {
        const int scale = 8, quiet = 4;
        int n = m.GetLength(0);
        int dim = (n + 2 * quiet) * scale;
        var gray = new byte[dim * dim];
        for (int i = 0; i < gray.Length; i++) gray[i] = 255;     // white
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (m[r, c])
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            gray[((r + quiet) * scale + dy) * dim + ((c + quiet) * scale + dx)] = 0;   // black

        var source = new RGBLuminanceSource(gray, dim, dim, RGBLuminanceSource.BitmapFormat.Gray8);
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions
            {
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                TryHarder = true,   // normal detector (finds the symbol via finder patterns + quiet zone)
            }
        };
        return reader.Decode(source)?.Text;
    }

    public static IEnumerable<object[]> RoundTripCases()
    {
        var texts = new[]
        {
            "A",
            "HELLO",
            "https://www.softvelocity.com",
            "Order #12345 - pay $9.99",
            "ID:CUS00042|2026-06-21|reddinassessments.com",
            new string('x', 120),                               // long-ish -> higher version
            new string('Z', 250),                               // near the v10-L ceiling
        };
        foreach (var t in texts)
            foreach (var ecc in new[] { Ecc.L, Ecc.M, Ecc.Q, Ecc.H })
                if (System.Text.Encoding.UTF8.GetByteCount(t) <= MaxBytes(ecc))   // skip cases that don't fit v1-10
                    yield return new object[] { t, ecc };
    }

    static int MaxBytes(Ecc ecc) => ecc switch { Ecc.L => 271, Ecc.M => 213, Ecc.Q => 151, _ => 119 };

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void Encode_ProducesAScannableCode_ThatDecodesBackToTheInput(string text, Ecc ecc)
    {
        var matrix = QrEncoder.Encode(text, ecc);
        Assert.Equal(text, Decode(matrix));
    }

    [Theory]
    [InlineData("A", 21)]                                       // version 1 -> 21x21
    [InlineData("https://www.softvelocity.com", 25)]            // 28 bytes at ECC L -> version 2 (25x25)
    public void Encode_PicksTheRightSizeForTheData(string text, int expectedDim)
    {
        var m = QrEncoder.Encode(text, Ecc.L);
        Assert.Equal(expectedDim, m.GetLength(0));
        Assert.Equal(expectedDim, m.GetLength(1));
    }

    [Fact]
    public void Encode_PlacesTheThreeFinderPatterns()
    {
        var m = QrEncoder.Encode("A", Ecc.M);
        int n = m.GetLength(0);
        Assert.True(IsFinder(m, 0, 0));            // top-left
        Assert.True(IsFinder(m, 0, n - 7));        // top-right
        Assert.True(IsFinder(m, n - 7, 0));        // bottom-left
    }

    static bool IsFinder(bool[,] m, int r, int c)
    {
        for (int dr = 0; dr < 7; dr++)
            for (int dc = 0; dc < 7; dc++)
            {
                bool dark = dr == 0 || dr == 6 || dc == 0 || dc == 6 || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4);
                if (m[r + dr, c + dc] != dark) return false;
            }
        return true;
    }

    [Fact]
    public void ChooseVersion_GrowsWithDataAndThrowsWhenTooLarge()
    {
        Assert.Equal(1, QrEncoder.ChooseVersion(10, Ecc.L));
        Assert.True(QrEncoder.ChooseVersion(200, Ecc.L) > QrEncoder.ChooseVersion(10, Ecc.L));
        Assert.Throws<ArgumentException>(() => QrEncoder.Encode(new string('x', 5000), Ecc.H));
    }

    [Fact]
    public void Encode_HandlesUtf8MultiByteText()
    {
        const string s = "cafe ☕ - 1€";              // multi-byte UTF-8 (coffee, euro)
        Assert.Equal(s, Decode(QrEncoder.Encode(s, Ecc.Q)));
    }
}
