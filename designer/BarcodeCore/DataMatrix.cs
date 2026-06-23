using System.Text;

namespace BarcodeCore;

/// <summary>
/// Data Matrix (ECC200) encoder — ASCII encodation, square symbols 10×10…26×26 (single data region).
/// Returns a square module matrix [row, col] where <c>true</c> = dark, no quiet zone. Reed–Solomon is over
/// GF(256) with primitive polynomial 0x12D and generator base 1 (the Data Matrix field, distinct from QR's
/// 0x11D / base 0). Validated by decoding the output with ZXing.
/// </summary>
public static class DataMatrix
{
    // symbol dim, data-region dim (= dim-2), data codewords, EC codewords  (single-region square sizes)
    static readonly (int Dim, int Region, int Data, int Ec)[] Sizes =
    {
        (10,8,3,5), (12,10,5,7), (14,12,8,10), (16,14,12,12), (18,16,18,14),
        (20,18,22,18), (22,20,30,20), (24,22,36,24), (26,24,44,28),
    };

    static readonly int[] Exp = new int[512];
    static readonly int[] Log = new int[256];
    static DataMatrix()
    {
        int x = 1;
        for (int i = 0; i < 255; i++) { Exp[i] = x; Log[x] = i; x <<= 1; if ((x & 0x100) != 0) x ^= 0x12D; }
        for (int i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
    }
    static int Mul(int a, int b) => (a == 0 || b == 0) ? 0 : Exp[Log[a] + Log[b]];

    public static bool[,] Encode(string text)
    {
        var data = EncodeAscii(text ?? "");
        int si = -1;
        for (int i = 0; i < Sizes.Length; i++) if (data.Count <= Sizes[i].Data) { si = i; break; }
        if (si < 0) throw new ArgumentException(
            $"Data Matrix: {data.Count} codewords exceed the 26×26 symbol (max {Sizes[^1].Data}). Use less data.");
        var sz = Sizes[si];

        if (data.Count < sz.Data)                       // pad: first 129, then the 253-state randomisation
        {
            data.Add(129);
            while (data.Count < sz.Data)
            {
                int r = (149 * (data.Count + 1)) % 253 + 1;
                int v = 129 + r;
                data.Add(v <= 254 ? v : v - 254);
            }
        }

        var ec = ReedSolomon(data.ToArray(), sz.Ec);
        var all = new int[sz.Data + sz.Ec];
        for (int i = 0; i < sz.Data; i++) all[i] = data[i];
        for (int i = 0; i < sz.Ec; i++) all[sz.Data + i] = ec[i];

        var region = Ecc200Placement(all, sz.Region, sz.Region);
        return AddFinder(region, sz.Region, sz.Dim);
    }

    // ---- ASCII encodation: digit pairs -> one codeword, else char+1 (upper-shift for >127) ----
    static List<int> EncodeAscii(string s)
    {
        var d = new List<int>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsDigit(c) && i + 1 < s.Length && char.IsDigit(s[i + 1]))
            {
                d.Add((c - '0') * 10 + (s[i + 1] - '0') + 130);
                i += 2;
            }
            else if (c < 128) { d.Add(c + 1); i++; }
            else { d.Add(235); d.Add((c - 128) + 1); i++; }   // upper shift
        }
        return d;
    }

    // ---- Reed–Solomon (GF 0x12D, generator base 1) ----
    static int[] ReedSolomon(int[] data, int ecLen)
    {
        var gen = RsGen(ecLen);
        var res = new int[ecLen];
        foreach (var dd in data)
        {
            int factor = dd ^ res[0];
            Array.Copy(res, 1, res, 0, ecLen - 1);
            res[ecLen - 1] = 0;
            for (int i = 0; i < ecLen; i++) res[i] ^= Mul(gen[i], factor);
        }
        return res;
    }
    static int[] RsGen(int degree)
    {
        var g = new int[] { 1 };
        for (int i = 0; i < degree; i++)        // roots a^1 .. a^degree  (generator base 1)
        {
            var ng = new int[g.Length + 1];
            for (int j = 0; j < g.Length; j++) { ng[j] ^= g[j]; ng[j + 1] ^= Mul(g[j], Exp[i + 1]); }
            g = ng;
        }
        var coeffs = new int[degree];
        Array.Copy(g, 1, coeffs, 0, degree);
        return coeffs;
    }

    // ---- ECC200 module placement (ISO/IEC 16022 Annex F) ----
    static bool[,] Ecc200Placement(int[] cw, int nrow, int ncol)
    {
        var bit = new int[nrow, ncol];
        var set = new bool[nrow, ncol];

        void Module(int row, int col, int pos, int b)
        {
            if (row < 0) { row += nrow; col += 4 - (nrow + 4) % 8; }
            if (col < 0) { col += ncol; row += 4 - (ncol + 4) % 8; }
            bit[row, col] = (cw[pos - 1] >> (8 - b)) & 1;
            set[row, col] = true;
        }
        void Utah(int row, int col, int pos)
        {
            Module(row - 2, col - 2, pos, 1); Module(row - 2, col - 1, pos, 2);
            Module(row - 1, col - 2, pos, 3); Module(row - 1, col - 1, pos, 4);
            Module(row - 1, col, pos, 5); Module(row, col - 2, pos, 6);
            Module(row, col - 1, pos, 7); Module(row, col, pos, 8);
        }
        void Corner1(int pos)
        {
            Module(nrow - 1, 0, pos, 1); Module(nrow - 1, 1, pos, 2); Module(nrow - 1, 2, pos, 3);
            Module(0, ncol - 2, pos, 4); Module(0, ncol - 1, pos, 5); Module(1, ncol - 1, pos, 6);
            Module(2, ncol - 1, pos, 7); Module(3, ncol - 1, pos, 8);
        }
        void Corner2(int pos)
        {
            Module(nrow - 3, 0, pos, 1); Module(nrow - 2, 0, pos, 2); Module(nrow - 1, 0, pos, 3);
            Module(0, ncol - 4, pos, 4); Module(0, ncol - 3, pos, 5); Module(0, ncol - 2, pos, 6);
            Module(0, ncol - 1, pos, 7); Module(1, ncol - 1, pos, 8);
        }
        void Corner3(int pos)
        {
            Module(nrow - 3, 0, pos, 1); Module(nrow - 2, 0, pos, 2); Module(nrow - 1, 0, pos, 3);
            Module(0, ncol - 2, pos, 4); Module(0, ncol - 1, pos, 5); Module(1, ncol - 1, pos, 6);
            Module(2, ncol - 1, pos, 7); Module(3, ncol - 1, pos, 8);
        }
        void Corner4(int pos)
        {
            Module(nrow - 1, 0, pos, 1); Module(nrow - 1, ncol - 1, pos, 2); Module(0, ncol - 3, pos, 3);
            Module(0, ncol - 2, pos, 4); Module(0, ncol - 1, pos, 5); Module(1, ncol - 3, pos, 6);
            Module(1, ncol - 2, pos, 7); Module(1, ncol - 1, pos, 8);
        }

        int p = 1, row = 4, col = 0;
        do
        {
            if (row == nrow && col == 0) Corner1(p++);
            if (row == nrow - 2 && col == 0 && ncol % 4 != 0) Corner2(p++);
            if (row == nrow - 2 && col == 0 && ncol % 8 == 4) Corner3(p++);
            if (row == nrow + 4 && col == 2 && ncol % 8 == 0) Corner4(p++);
            do
            {
                if (row < nrow && col >= 0 && !set[row, col]) Utah(row, col, p++);
                row -= 2; col += 2;
            } while (row >= 0 && col < ncol);
            row += 1; col += 3;
            do
            {
                if (row >= 0 && col < ncol && !set[row, col]) Utah(row, col, p++);
                row += 2; col -= 2;
            } while (row < nrow && col >= 0);
            row += 3; col += 1;
        } while (row < nrow || col < ncol);

        if (!set[nrow - 1, ncol - 1])                       // the special bottom-right corner
        {
            bit[nrow - 1, ncol - 1] = 1; bit[nrow - 2, ncol - 2] = 1;
            set[nrow - 1, ncol - 1] = true; set[nrow - 2, ncol - 2] = true;
        }

        var g = new bool[nrow, ncol];
        for (int r = 0; r < nrow; r++) for (int c = 0; c < ncol; c++) g[r, c] = bit[r, c] == 1;
        return g;
    }

    // ---- wrap the data region with the finder/timing border -> full symbol ----
    static bool[,] AddFinder(bool[,] region, int n, int dim)
    {
        var m = new bool[dim, dim];
        for (int r = 0; r < n; r++) for (int c = 0; c < n; c++) m[r + 1, c + 1] = region[r, c];
        for (int r = 0; r < dim; r++) { m[r, 0] = true; m[r, dim - 1] = (r % 2 == 1); }     // left solid, right timing
        for (int c = 0; c < dim; c++) { m[dim - 1, c] = true; m[0, c] = (c % 2 == 0); }     // bottom solid, top timing
        return m;
    }
}
