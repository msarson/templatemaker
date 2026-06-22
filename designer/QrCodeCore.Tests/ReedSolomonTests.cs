using QrCodeCore;
using Xunit;

namespace QrCodeCore.Tests;

public class ReedSolomonTests
{
    // The canonical worked example from ISO/IEC 18004 (Version 1-M, numeric "01234567"):
    // 16 data codewords -> 10 EC codewords. Pins down the Galois field + generator polynomial.
    [Fact]
    public void ComputeEcc_MatchesTheIsoAnnexExample()
    {
        byte[] data = { 0x10, 0x20, 0x0C, 0x56, 0x61, 0x80, 0xEC, 0x11, 0xEC, 0x11, 0xEC, 0x11, 0xEC, 0x11, 0xEC, 0x11 };
        byte[] expected = { 0xA5, 0x24, 0xD4, 0xC1, 0xED, 0x36, 0xC7, 0x87, 0x2C, 0x55 };
        Assert.Equal(expected, QrEncoder.ComputeEcc(data, 10));
    }
}
