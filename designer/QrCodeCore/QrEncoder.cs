using System;
using System.Collections.Generic;
using System.Text;

namespace QrCodeCore;

/// <summary>Error-correction level. The numeric value is the enum index, NOT the QR format indicator.</summary>
public enum Ecc { L, M, Q, H }

/// <summary>
/// A self-contained QR encoder — byte (8-bit) mode, versions 1–10, all four ECC levels, automatic version
/// selection and automatic mask. <see cref="Encode(string,Ecc)"/> returns a square module matrix indexed
/// [row, col] where <c>true</c> = dark. No quiet zone is added (the caller draws that). This is a reference
/// implementation to port to a Clarion drawing template; it is validated by decoding its output with ZXing.
/// </summary>
public static class QrEncoder
{
    public const int MinVersion = 1;
    public const int MaxVersion = 10;

    public static bool[,] Encode(string text, Ecc ecc) => Encode(Encoding.UTF8.GetBytes(text ?? ""), ecc);

    public static bool[,] Encode(byte[] data, Ecc ecc)
    {
        int version = ChooseVersion(data.Length, ecc);
        byte[] dataCodewords = BuildDataCodewords(data, version, ecc);
        byte[] all = InterleaveWithEcc(dataCodewords, version, ecc);

        int n = 17 + 4 * version;
        var modules = new bool[n, n];
        var func = new bool[n, n];
        DrawFunctionPatterns(modules, func, version);
        DrawCodewords(modules, func, all, n);

        int bestMask = ApplyBestMask(modules, func, version, ecc);
        DrawFormatInfo(modules, func, ecc, bestMask, n);     // final format info for the chosen mask
        return modules;
    }

    // ---- version / capacity -------------------------------------------------

    public static int ChooseVersion(int dataLen, Ecc ecc)
    {
        for (int v = MinVersion; v <= MaxVersion; v++)
            if (dataLen <= ByteCapacity(v, ecc)) return v;
        throw new ArgumentException(
            $"Data is {dataLen} bytes — too large for QR versions {MinVersion}–{MaxVersion} at ECC {ecc} " +
            $"(max {ByteCapacity(MaxVersion, ecc)} bytes). Use a higher ECC budget or shorter data.");
    }

    static int ByteCapacity(int version, Ecc ecc)
    {
        int dataBits = DataCodewords(version, ecc) * 8;
        int avail = dataBits - 4 - CharCountBits(version);   // mode indicator (4) + char-count field
        return Math.Max(0, avail / 8);
    }

    static int CharCountBits(int version) => version <= 9 ? 8 : 16;   // byte mode: 8 bits (v1–9), 16 (v10+)

    // ---- data codewords (mode + count + data + padding) ---------------------

    static byte[] BuildDataCodewords(byte[] data, int version, Ecc ecc)
    {
        int totalData = DataCodewords(version, ecc);
        var bits = new BitBuffer();
        bits.Append(0b0100, 4);                              // byte mode indicator
        bits.Append(data.Length, CharCountBits(version));    // character count
        foreach (var b in data) bits.Append(b, 8);

        int capacityBits = totalData * 8;
        int terminator = Math.Min(4, capacityBits - bits.Length);
        bits.Append(0, terminator);                          // terminator (up to 4 zero bits)
        if (bits.Length % 8 != 0) bits.Append(0, 8 - bits.Length % 8);   // pad to a byte boundary

        var bytes = bits.ToBytes();
        int pad = 0;
        for (int i = bytes.Count; i < totalData; i++)        // alternating pad bytes 0xEC / 0x11
            bytes.Add((byte)((pad++ % 2 == 0) ? 0xEC : 0x11));
        return bytes.ToArray();
    }

    // ---- error correction + interleaving ------------------------------------

    static byte[] InterleaveWithEcc(byte[] dataCw, int version, Ecc ecc)
    {
        var e = Table[version - 1, (int)ecc];
        int ecPerBlock = e.EcPerBlock;

        // Split the data codewords into the group-1 / group-2 blocks.
        var dataBlocks = new List<byte[]>();
        var ecBlocks = new List<byte[]>();
        int pos = 0;
        for (int b = 0; b < e.G1Blocks; b++) { var blk = Slice(dataCw, pos, e.G1Data); pos += e.G1Data; dataBlocks.Add(blk); ecBlocks.Add(ReedSolomon(blk, ecPerBlock)); }
        for (int b = 0; b < e.G2Blocks; b++) { var blk = Slice(dataCw, pos, e.G2Data); pos += e.G2Data; dataBlocks.Add(blk); ecBlocks.Add(ReedSolomon(blk, ecPerBlock)); }

        var outBytes = new List<byte>();
        int maxData = Math.Max(e.G1Data, e.G2Data);
        for (int i = 0; i < maxData; i++)                    // interleave data codewords column-wise
            foreach (var blk in dataBlocks) if (i < blk.Length) outBytes.Add(blk[i]);
        for (int i = 0; i < ecPerBlock; i++)                 // then interleave EC codewords
            foreach (var blk in ecBlocks) outBytes.Add(blk[i]);
        return outBytes.ToArray();
    }

    static byte[] Slice(byte[] src, int start, int len) { var r = new byte[len]; Array.Copy(src, start, r, 0, len); return r; }

    // ---- module placement ---------------------------------------------------

    static void DrawFunctionPatterns(bool[,] m, bool[,] f, int version)
    {
        int n = m.GetLength(0);
        // Timing patterns
        for (int i = 0; i < n; i++) { Set(m, f, 6, i, i % 2 == 0); Set(m, f, i, 6, i % 2 == 0); }
        // Finder patterns + separators (3 corners)
        DrawFinder(m, f, 0, 0); DrawFinder(m, f, 0, n - 7); DrawFinder(m, f, n - 7, 0);
        // Alignment patterns
        var pos = AlignPositions(version);
        foreach (var r in pos) foreach (var c in pos)
        {
            if ((r <= 7 && c <= 7) || (r <= 7 && c >= n - 8) || (r >= n - 8 && c <= 7)) continue;   // skip finder corners
            DrawAlignment(m, f, r, c);
        }
        // Dark module
        Set(m, f, n - 8, 8, true);
        // Reserve format-info area (drawn for real after masking) and version-info area (v7+)
        ReserveFormat(f, n);
        if (version >= 7) DrawVersionInfo(m, f, version, n);
    }

    static void DrawFinder(bool[,] m, bool[,] f, int r, int c)
    {
        for (int dr = -1; dr <= 7; dr++) for (int dc = -1; dc <= 7; dc++)
        {
            int rr = r + dr, cc = c + dc;
            if (rr < 0 || rr >= m.GetLength(0) || cc < 0 || cc >= m.GetLength(0)) continue;
            bool dark = dr >= 0 && dr <= 6 && dc >= 0 && dc <= 6 &&
                        (dr == 0 || dr == 6 || dc == 0 || dc == 6 || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4));
            Set(m, f, rr, cc, dark);
        }
    }

    static void DrawAlignment(bool[,] m, bool[,] f, int r, int c)
    {
        for (int dr = -2; dr <= 2; dr++) for (int dc = -2; dc <= 2; dc++)
        {
            bool dark = Math.Max(Math.Abs(dr), Math.Abs(dc)) != 1;   // 5x5 ring with a centre dot
            Set(m, f, r + dr, c + dc, dark);
        }
    }

    static void ReserveFormat(bool[,] f, int n)
    {
        for (int i = 0; i <= 8; i++) { f[8, i] = true; f[i, 8] = true; }
        for (int i = 0; i < 8; i++) { f[8, n - 1 - i] = true; f[n - 1 - i, 8] = true; }
    }

    static void DrawVersionInfo(bool[,] m, bool[,] f, int version, int n)
    {
        int rem = version;                                    // 18-bit version info (6 data + 12 BCH, gen 0x1F25)
        for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        int bits = (version << 12) | rem;
        for (int i = 0; i < 18; i++)
        {
            bool b = GetBit(bits, i);
            int a = i / 3, c = i % 3;
            Set(m, f, n - 11 + c, a, b);
            Set(m, f, a, n - 11 + c, b);
        }
    }

    static void DrawFormatInfo(bool[,] m, bool[,] f, Ecc ecc, int mask, int n)
    {
        int data = (FormatEccBits(ecc) << 3) | mask;          // 5 bits (ecc indicator + mask)
        int rem = data;                                       // 15-bit format info (5 data + 10 BCH, gen 0x537)
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        int bits = ((data << 10) | rem) ^ 0x5412;             // XOR mask so an all-zero format isn't all-light
        // first copy: down column 8 (rows 0-5,7,8), then along row 8 (cols 7,5..0). (row, col) per the spec.
        for (int i = 0; i <= 5; i++) m[i, 8] = GetBit(bits, i);
        m[7, 8] = GetBit(bits, 6); m[8, 8] = GetBit(bits, 7); m[8, 7] = GetBit(bits, 8);
        for (int i = 9; i < 15; i++) m[8, 14 - i] = GetBit(bits, i);
        // second copy: along row 8 (cols n-1..n-8), then up column 8 (rows n-7..n-1).
        for (int i = 0; i < 8; i++) m[8, n - 1 - i] = GetBit(bits, i);
        for (int i = 8; i < 15; i++) m[n - 15 + i, 8] = GetBit(bits, i);
        m[n - 8, 8] = true;                                   // the always-dark module
    }

    static bool GetBit(int x, int i) => ((x >> i) & 1) != 0;

    static void DrawCodewords(bool[,] m, bool[,] f, byte[] codewords, int n)
    {
        int bit = 0, total = codewords.Length * 8;
        for (int col = n - 1; col > 0; col -= 2)
        {
            if (col == 6) col--;   // skip the vertical timing column
            for (int t = 0; t < n; t++)
            {
                bool upward = ((n - 1 - col) / 2 % 2) == 0;   // even pair-columns go up, odd go down
                int row = upward ? n - 1 - t : t;
                for (int c = 0; c < 2; c++)
                {
                    int cc = col - c;
                    if (f[row, cc]) continue;
                    bool dark = bit < total && ((codewords[bit >> 3] >> (7 - (bit & 7))) & 1) != 0;
                    m[row, cc] = dark; bit++;
                }
            }
        }
    }

    // ---- masking ------------------------------------------------------------

    static int ApplyBestMask(bool[,] m, bool[,] f, int version, Ecc ecc)
    {
        int n = m.GetLength(0), best = 0, bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            ApplyMask(m, f, mask, n);
            DrawFormatInfo(m, f, ecc, mask, n);               // penalty rule 3 looks at the whole symbol
            int p = Penalty(m, n);
            if (p < bestPenalty) { bestPenalty = p; best = mask; }
            ApplyMask(m, f, mask, n);                         // XOR again to revert (mask is its own inverse)
        }
        ApplyMask(m, f, best, n);                             // leave the winning mask applied
        return best;
    }

    static void ApplyMask(bool[,] m, bool[,] f, int mask, int n)
    {
        for (int r = 0; r < n; r++) for (int c = 0; c < n; c++)
        {
            if (f[r, c]) continue;
            if (MaskBit(mask, r, c)) m[r, c] = !m[r, c];
        }
    }

    static bool MaskBit(int mask, int r, int c) => mask switch
    {
        0 => (r + c) % 2 == 0,
        1 => r % 2 == 0,
        2 => c % 3 == 0,
        3 => (r + c) % 3 == 0,
        4 => (r / 2 + c / 3) % 2 == 0,
        5 => (r * c) % 2 + (r * c) % 3 == 0,
        6 => ((r * c) % 2 + (r * c) % 3) % 2 == 0,
        _ => ((r + c) % 2 + (r * c) % 3) % 2 == 0,
    };

    static int Penalty(bool[,] m, int n)
    {
        int p = 0;
        // Rule 1: runs of 5+ same colour in each row and column.
        for (int r = 0; r < n; r++) p += LineRuns(m, n, r, true);
        for (int c = 0; c < n; c++) p += LineRuns(m, n, c, false);
        // Rule 2: 2x2 blocks of one colour.
        for (int r = 0; r < n - 1; r++) for (int c = 0; c < n - 1; c++)
            if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1]) p += 3;
        // Rule 3: 1:1:3:1:1 finder-like pattern with 4 light modules on one side, in rows and columns.
        for (int r = 0; r < n; r++)
            for (int c = 0; c <= n - 11; c++)
                if (MatchFinderLike(m, r, c, true)) p += 40;     // horizontal: reads m[r, c+i]
        for (int c = 0; c < n; c++)
            for (int r = 0; r <= n - 11; r++)
                if (MatchFinderLike(m, c, r, false)) p += 40;    // vertical: reads m[r+i, c]
        // Rule 4: deviation of the dark-module proportion from 50%.
        int dark = 0; foreach (var b in m) if (b) dark++;
        int prev = (dark * 100 / (n * n)) / 5 * 5;
        p += Math.Min(Math.Abs(prev - 50), Math.Abs(prev + 5 - 50)) / 5 * 10;
        return p;
    }

    static int LineRuns(bool[,] m, int n, int idx, bool row)
    {
        int p = 0, run = 1;
        bool prev = row ? m[idx, 0] : m[0, idx];
        for (int k = 1; k < n; k++)
        {
            bool cur = row ? m[idx, k] : m[k, idx];
            if (cur == prev) { run++; }
            else { if (run >= 5) p += 3 + (run - 5); run = 1; prev = cur; }
        }
        if (run >= 5) p += 3 + (run - 5);
        return p;
    }

    static readonly bool[] FinderA = { true, false, true, true, true, false, true, false, false, false, false };
    static readonly bool[] FinderB = { false, false, false, false, true, false, true, true, true, false, true };
    static bool MatchFinderLike(bool[,] m, int a, int b, bool row)
        => MatchExact(m, a, b, row, FinderA) || MatchExact(m, a, b, row, FinderB);
    static bool MatchExact(bool[,] m, int a, int b, bool row, bool[] pat)
    {
        for (int i = 0; i < 11; i++) { bool v = row ? m[a, b + i] : m[b + i, a]; if (v != pat[i]) return false; }
        return true;
    }

    static void Set(bool[,] m, bool[,] f, int r, int c, bool v) { m[r, c] = v; f[r, c] = true; }

    // ---- Galois field GF(256) + Reed-Solomon --------------------------------

    static readonly int[] Exp = new int[512];
    static readonly int[] Log = new int[256];
    static QrEncoder()
    {
        int x = 1;
        for (int i = 0; i < 255; i++) { Exp[i] = x; Log[x] = i; x <<= 1; if ((x & 0x100) != 0) x ^= 0x11D; }
        for (int i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
    }
    static int GfMul(int a, int b) => (a == 0 || b == 0) ? 0 : Exp[Log[a] + Log[b]];

    /// <summary>Reed-Solomon EC codewords for a data block — exposed for tests/diagnostics.</summary>
    public static byte[] ComputeEcc(byte[] data, int ecLen) => ReedSolomon(data, ecLen);

    static byte[] ReedSolomon(byte[] data, int ecLen)
    {
        var gen = RsGenerator(ecLen);
        var res = new int[ecLen];
        foreach (var d in data)
        {
            int factor = d ^ res[0];
            Array.Copy(res, 1, res, 0, ecLen - 1);
            res[ecLen - 1] = 0;
            for (int i = 0; i < ecLen; i++) res[i] ^= GfMul(gen[i], factor);
        }
        var outBytes = new byte[ecLen];
        for (int i = 0; i < ecLen; i++) outBytes[i] = (byte)res[i];
        return outBytes;
    }

    static readonly Dictionary<int, int[]> GenCache = new();
    static int[] RsGenerator(int degree)
    {
        if (GenCache.TryGetValue(degree, out var cached)) return cached;
        var g = new int[] { 1 };
        for (int i = 0; i < degree; i++)
        {
            var ng = new int[g.Length + 1];
            for (int j = 0; j < g.Length; j++) { ng[j] ^= g[j]; ng[j + 1] ^= GfMul(g[j], Exp[i]); }
            g = ng;
        }
        // g currently includes the leading 1; the encoder wants the `degree` trailing coefficients.
        var coeffs = new int[degree];
        Array.Copy(g, 1, coeffs, 0, degree);
        GenCache[degree] = coeffs;
        return coeffs;
    }

    // QR format ECC indicator bits (NOT the enum order): M=00, L=01, H=10, Q=11.
    static int FormatEccBits(Ecc ecc) => ecc switch { Ecc.M => 0, Ecc.L => 1, Ecc.H => 2, _ => 3 };

    // ---- tables -------------------------------------------------------------

    static int[] AlignPositions(int version) => version switch
    {
        1 => Array.Empty<int>(),
        2 => new[] { 6, 18 }, 3 => new[] { 6, 22 }, 4 => new[] { 6, 26 }, 5 => new[] { 6, 30 },
        6 => new[] { 6, 34 }, 7 => new[] { 6, 22, 38 }, 8 => new[] { 6, 24, 42 }, 9 => new[] { 6, 26, 46 },
        _ => new[] { 6, 28, 50 },   // version 10
    };

    static int DataCodewords(int version, Ecc ecc)
    {
        var e = Table[version - 1, (int)ecc];
        return e.G1Blocks * e.G1Data + e.G2Blocks * e.G2Data;
    }

    readonly record struct Ecb(int EcPerBlock, int G1Blocks, int G1Data, int G2Blocks, int G2Data);

    // [version-1, ecc(L=0,M=1,Q=2,H=3)] -> EC-codewords-per-block, group-1 blocks/data, group-2 blocks/data.
    static readonly Ecb[,] Table = new Ecb[10, 4]
    {
        /* v1  */ { new(7,1,19,0,0),  new(10,1,16,0,0), new(13,1,13,0,0), new(17,1,9,0,0) },
        /* v2  */ { new(10,1,34,0,0), new(16,1,28,0,0), new(22,1,22,0,0), new(28,1,16,0,0) },
        /* v3  */ { new(15,1,55,0,0), new(26,1,44,0,0), new(18,2,17,0,0), new(22,2,13,0,0) },
        /* v4  */ { new(20,1,80,0,0), new(18,2,32,0,0), new(26,2,24,0,0), new(16,4,9,0,0) },
        /* v5  */ { new(26,1,108,0,0),new(24,2,43,0,0), new(18,2,15,2,16),new(22,2,11,2,12) },
        /* v6  */ { new(18,2,68,0,0), new(16,4,27,0,0), new(24,4,19,0,0), new(28,4,15,0,0) },
        /* v7  */ { new(20,2,78,0,0), new(18,4,31,0,0), new(18,2,14,4,15),new(26,4,13,1,14) },
        /* v8  */ { new(24,2,97,0,0), new(22,2,38,2,39),new(22,4,18,2,19),new(26,4,14,2,15) },
        /* v9  */ { new(30,2,116,0,0),new(22,3,36,2,37),new(20,4,16,4,17),new(24,4,12,4,13) },
        /* v10 */ { new(18,2,68,2,69),new(26,4,43,1,44),new(24,6,19,2,20),new(28,6,15,2,16) },
    };

    sealed class BitBuffer
    {
        readonly List<bool> _bits = new();
        public int Length => _bits.Count;
        public void Append(int value, int bits) { for (int i = bits - 1; i >= 0; i--) _bits.Add(((value >> i) & 1) != 0); }
        public List<byte> ToBytes()
        {
            var bytes = new List<byte>();
            for (int i = 0; i < _bits.Count; i += 8)
            {
                int b = 0;
                for (int j = 0; j < 8 && i + j < _bits.Count; j++) if (_bits[i + j]) b |= 1 << (7 - j);
                bytes.Add((byte)b);
            }
            return bytes;
        }
    }
}
