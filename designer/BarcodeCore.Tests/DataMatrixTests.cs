using BarcodeCore;
using Xunit;
using Xunit.Abstractions;
using ZXing;
using ZXing.Common;

namespace BarcodeCore.Tests;

public class DataMatrixTests
{
    readonly ITestOutputHelper _out;
    public DataMatrixTests(ITestOutputHelper o) => _out = o;

    static string? Decode(bool[,] m)
    {
        const int scale = 8, quiet = 2;
        int n = m.GetLength(0);
        int dim = (n + 2 * quiet) * scale;
        var gray = new byte[dim * dim];
        Array.Fill(gray, (byte)255);
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (m[r, c])
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            gray[((r + quiet) * scale + dy) * dim + ((c + quiet) * scale + dx)] = 0;
        var src = new RGBLuminanceSource(gray, dim, dim, RGBLuminanceSource.BitmapFormat.Gray8);
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions
            {
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.DATA_MATRIX },
                TryHarder = true,
                PureBarcode = true,
            }
        };
        return reader.Decode(src)?.Text;
    }

    [Theory]
    [InlineData("HELLO")]
    [InlineData("Data Matrix!")]
    [InlineData("12345678")]
    [InlineData("ABC-123-XYZ")]
    [InlineData("https://example.com")]
    [InlineData("The quick brown fox jumps")]
    public void RoundTrips(string text)
        => Assert.Equal(text, Decode(DataMatrix.Encode(text)));

    [Fact]
    public void SmallText_PicksSmallSquare()
        => Assert.Equal(10, DataMatrix.Encode("AB").GetLength(0));   // 2 chars -> 10x10
}
