using System.Buffers.Binary;
using System.Numerics;

namespace RelicWriter.GameAssets;

/// <summary>
/// Managed decoder for the Oodle "Kraken" compression the game's archives use, so reading the game files works on every platform
/// (the game's own Oodle DLL is Windows only). Written from the observed bitstream, see docs/kraken.md. Returns false for anything
/// it does not understand (so far: sparse tANS tables and the old Huffman header); callers treat that like a corrupt file.
/// </summary>
internal static class Kraken
{
    const int BlockSize = 0x40000, ChunkSize = 0x20000, MaxCodeLength = 11;

    /// <summary>Decompresses exactly <paramref name="dst"/>.Length bytes from a raw Kraken stream (without the archive's KARK header).</summary>
    public static bool TryDecompress(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        try { return Decompress(src, dst); }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidDataException) { return false; }
    }

    static bool Decompress(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        int sp = 0, dp = 0;
        while (dp < dst.Length)
        {
            // Every 256 KB starts with a two byte header: low nibble 0xC, bit 6 of the first byte = stored (bit 7 = a keyframe; bit 5 is not understood), decoder type 6 (Kraken) in the second.
            if ((dp & (BlockSize - 1)) == 0)
            {
                if (src.Length - sp < 2 || (src[sp] & 0x2F) != 0xC || (src[sp + 1] & 0x7F) != 6) return false;
                var stored = (src[sp] & 0x40) != 0;
                sp += 2;
                if (stored)
                {
                    var n = Math.Min(BlockSize, dst.Length - dp);
                    if (src.Length - sp < n) return false;
                    src.Slice(sp, n).CopyTo(dst[dp..]);
                    sp += n; dp += n;
                    continue;
                }
            }
            var left = Math.Min(BlockSize, dst.Length - dp);
            if (src.Length - sp < 3) return false;
            var v = src[sp] << 16 | src[sp + 1] << 8 | src[sp + 2];
            var size = v & 0x3FFFF;
            if (size == 0x3FFFF)
            {
                // One byte repeated over the whole block.
                if (v >> 18 != 1 || src.Length - sp < 4) return false;
                dst.Slice(dp, left).Fill(src[sp + 3]);
                sp += 4; dp += left;
                continue;
            }
            sp += 3;
            var compressed = size + 1;
            if (src.Length - sp < compressed) return false;
            var block = src.Slice(sp, compressed);
            if (compressed == left) block.CopyTo(dst[dp..]);
            else if (!DecodeBlock(block, dst, dp, left)) return false;
            sp += compressed; dp += left;
        }
        return true;
    }

    /// <summary>A 256 KB block: up to two 128 KB chunks, each either entropy coded bytes or an LZ chunk.</summary>
    static bool DecodeBlock(ReadOnlySpan<byte> src, Span<byte> dst, int dp, int count)
    {
        var sp = 0;
        for (var done = 0; done < count; done += ChunkSize)
        {
            var n = Math.Min(ChunkSize, count - done);
            if (src.Length - sp < 3) return false;
            var h = src[sp] << 16 | src[sp + 1] << 8 | src[sp + 2];
            if ((h & 0x800000) == 0)
            {
                // No matches: the chunk is one entropy coded block.
                var bytes = DecodeBytes(src, ref sp, n);
                if (bytes is null || bytes.Length != n) return false;
                bytes.CopyTo(dst[(dp + done)..]);
                continue;
            }
            var used = h & 0x7FFFF;
            var mode = (h >> 19) & 0xF;
            sp += 3;
            if (src.Length - sp < used) return false;
            var body = src.Slice(sp, used);
            sp += used;
            if (used >= n) body[..n].CopyTo(dst[(dp + done)..]);
            else if (mode > 1 || !DecodeLzChunk(body, dst, dp + done, n, mode)) return false;
        }
        return sp == src.Length;
    }

    // ---- entropy coded byte blocks ----

    /// <summary>
    /// Reads one block (3 or 5 byte header: type, packed and real size) and returns its real bytes; advances <paramref name="pos"/>.
    /// Types: 0 stored, 1 tANS (dense table form only), 2 and 4 Huffman, 3 run length, 5 several blocks one after the other.
    /// </summary>
    static byte[]? DecodeBytes(ReadOnlySpan<byte> src, ref int pos, int maxSize)
    {
        if (src.Length - pos < 2) return null;
        var type = (src[pos] >> 4) & 7;
        int packed, real, header;
        if (type == 0)
        {
            if (src[pos] >= 0x80) { packed = (src[pos] << 8 | src[pos + 1]) & 0xFFF; header = 2; }
            else
            {
                if (src.Length - pos < 3) return null;
                packed = src[pos] << 16 | src[pos + 1] << 8 | src[pos + 2];
                header = 3;
            }
            real = packed;
        }
        else if (src[pos] >= 0x80)
        {
            if (src.Length - pos < 3) return null;
            var bits = src[pos] << 16 | src[pos + 1] << 8 | src[pos + 2];
            packed = bits & 0x3FF;
            real = packed + ((bits >> 10) & 0x3FF) + 1;
            header = 3;
        }
        else
        {
            if (src.Length - pos < 5) return null;
            var x = BinaryPrimitives.ReadUInt32BigEndian(src[(pos + 1)..]);
            packed = (int)(x & 0x3FFFF);
            real = (int)((((x >> 18) | ((uint)src[pos] << 14)) & 0x3FFFF) + 1);
            header = 5;
        }
        if (real > maxSize || src.Length - pos - header < packed) return null;
        var payload = src.Slice(pos + header, packed);
        pos += header + packed;
        return type switch
        {
            0 => payload.ToArray(),
            1 => DecodeTans(payload, real),
            2 or 4 => DecodeHuffman(payload, real, type == 4),
            3 => DecodeRle(payload, real),
            5 => DecodeSequence(payload, real),
            _ => null,
        };
    }

    /// <summary>Type 5: a count (bit 7 clear), then that many blocks whose bytes are joined.</summary>
    static byte[]? DecodeSequence(ReadOnlySpan<byte> src, int real)
    {
        if (src.Length < 2 || (src[0] & 0x80) != 0 || (src[0] & 0x7F) < 2) return null;
        var parts = src[0] & 0x7F;
        var result = new byte[real];
        int pos = 1, filled = 0;
        for (var i = 0; i < parts; i++)
        {
            var part = DecodeBytes(src, ref pos, real - filled);
            if (part is null) return null;
            part.CopyTo(result, filled);
            filled += part.Length;
        }
        return filled == real ? result : null;
    }

    /// <summary>
    /// Type 3. The buffer holds the literal bytes from the front and the commands from the back (read backwards). A command is some literals
    /// to copy and a run of the current run byte (0 until command 1 sets it from the literals). If the first byte is not 0 the front part is
    /// itself a block, decoded and put in front of the rest.
    /// </summary>
    static byte[]? DecodeRle(ReadOnlySpan<byte> src, int real)
    {
        if (src.Length <= 1) return src.Length == 1 ? Enumerable.Repeat(src[0], real).ToArray() : null;
        byte[] buf;
        int lp;
        if (src[0] != 0)
        {
            var pos = 0;
            var front = DecodeBytes(src, ref pos, real + src.Length);
            if (front is null) return null;
            buf = new byte[front.Length + src.Length - pos];
            front.CopyTo(buf, 0);
            src[pos..].CopyTo(buf.AsSpan(front.Length));
            lp = 0;
        }
        else { buf = src.ToArray(); lp = 1; }

        var result = new byte[real];
        int end = buf.Length, o = 0;
        byte run = 0;
        while (end > lp)
        {
            int copy = 0, repeat = 0;
            var cmd = buf[end - 1];
            if (cmd == 0 || cmd >= 0x30) { end--; copy = (-1 - cmd) & 0xF; repeat = cmd >> 4; }
            else if (cmd >= 0x10)
            {
                var data = (buf[end - 2] | buf[end - 1] << 8) - 4096;
                end -= 2; copy = data & 0x3F; repeat = data >> 6;
            }
            else if (cmd == 1) { run = buf[lp++]; end--; continue; }
            else if (cmd >= 9) { repeat = ((buf[end - 2] | buf[end - 1] << 8) - 0x8FF) * 128; end -= 2; }
            else { copy = ((buf[end - 2] | buf[end - 1] << 8) - 511) * 64; end -= 2; }
            if (copy < 0 || repeat < 0 || o + copy + repeat > real || lp + copy > end) return null;
            buf.AsSpan(lp, copy).CopyTo(result.AsSpan(o));
            lp += copy; o += copy;
            result.AsSpan(o, repeat).Fill(run);
            o += repeat;
        }
        return o == real ? result : null;
    }

    // ---- tANS ----

    /// <summary>
    /// Type 1. Header: a 0 bit, 2 bits for the table size 2^(8 + n), then the weights of the symbols (they add up to the table size). Only the
    /// dense form is understood (bit 0, then 3 bits = number of listed symbols - 1, the width of the weight steps, and per symbol 8 bits
    /// of symbol and a step added to the running weight; the last symbol has what is left). The rest is five interleaved states: the first
    /// five states come from the front stream (states 0, 2, 4) and the back stream (1, 3), a symbol is the symbol of its state's table
    /// entry, and the state then continues at the entry's base plus that many new bits, read from the front stream in even rounds of five
    /// symbols and from the back stream in odd rounds. The last five bytes of the output are simply the five states at the end.
    /// </summary>
    static byte[]? DecodeTans(ReadOnlySpan<byte> src, int real)
    {
        if (real < 5 || src.Length < 4) return null;
        var r = new MsbReader(src);
        if (r.Read(1) != 0) return null;
        var tableBits = 8 + r.Read(2);
        var size = 1 << tableBits;
        var single = new List<int>();
        var heavy = new List<(int Symbol, int Weight)>();
        if (r.Read(1) != 0)
        {
            // Sparse: the symbols present as in a Huffman header, then each symbol's weight as a Golomb-Rice number (its unary part comes
            // first, its low bits after the symbol ranges), adaptively mapped around a running average.
            var q = r.Read(3);
            var count = r.Read(8) + 1;
            if (count < 2) return null;
            var fluff = ReadFluff(ref r, count);
            var unary = new int[count + fluff];
            for (var i = 0; i < unary.Length; i++) unary[i] = r.ReadUnary();
            var present = ReadSymbolRanges(ref r, unary, count, fluff);
            if (present is null) return null;
            int average = 6, sum = 0;
            for (var i = 0; i < count; i++)
            {
                var extra = unary[i] + q;
                if (extra > 15) return null;
                var v = r.Read(extra) + (1 << extra) - (1 << q);
                var quarter = average >> 2;
                var limit = 2 * quarter;
                if (v <= limit) v = quarter + ((v >> 1) ^ -(v & 1));
                if (limit > v) limit = v;
                v++;
                average += limit - quarter;
                if (v == 1) single.Add(present[i]); else heavy.Add((present[i], v));
                sum += v;
            }
            if (r.Overrun || sum != size) return null;
        }
        else
        {
            var listed = r.Read(3) + 1;
            var stepBits = r.Read(BitOperations.Log2((uint)tableBits) + 1);
            if (stepBits == 0 || stepBits > tableBits) return null;
            int weight = 0, total = 0;
            for (var i = 0; i < listed; i++)
            {
                var symbol = r.Read(8);
                weight += r.Read(stepBits);
                if (weight == 0) return null;
                if (weight == 1) single.Add(symbol); else heavy.Add((symbol, weight));
                total += weight;
            }
            var lastSymbol = r.Read(8);
            if (r.Overrun || size - total < weight || size - total <= 1) return null;
            heavy.Add((lastSymbol, size - total));
        }
        var headerBytes = (r.Position + 7) >> 3;
        if (src.Length - headerBytes < 8) return null;

        // The table: four regions of the state space; symbols in order of symbol value take their share of every region.
        var symbols = new byte[size];
        var bits = new byte[size];
        var bases = new ushort[size];
        var slots = size - single.Count;
        var cursor = new int[4];
        for (var j = 1; j < 4; j++) cursor[j] = cursor[j - 1] + (slots >> 2) + (j - 1 < (slots & 3) ? 1 : 0);
        single.Sort();
        for (var i = 0; i < single.Count; i++) { symbols[slots + i] = (byte)single[i]; bits[slots + i] = (byte)tableBits; }
        heavy.Sort((x, y) => x.Symbol.CompareTo(y.Symbol));
        var offset = 0;
        foreach (var (symbol, w) in heavy)
        {
            if (w > 4)
            {
                var rank = w;
                for (var j = 0; j < 4; j++)
                {
                    var count = (w + ((offset - j - 1) & 3)) >> 2;
                    for (var t = 0; t < count; t++, rank++)
                    {
                        var at = cursor[j]++;
                        if (at >= size) return null;
                        var n = tableBits - BitOperations.Log2((uint)rank);
                        symbols[at] = (byte)symbol; bits[at] = (byte)n; bases[at] = (ushort)((size - 1) & (rank << n));
                    }
                }
            }
            else
            {
                var mask = ((1 << w) - 1) << (offset & 3);
                mask |= mask >> 4;
                var rank = w;
                for (var t = 0; t < w; t++, rank++)
                {
                    var j = BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                    var at = cursor[j]++;
                    if (j > 3 || at >= size) return null;
                    var n = tableBits - BitOperations.Log2((uint)rank);
                    symbols[at] = (byte)symbol; bits[at] = (byte)n; bases[at] = (ushort)((size - 1) & (rank << n));
                }
            }
            offset += w;
        }

        var data = src[headerBytes..];
        var front = new LsbReader(data, 0, forward: true);
        var back = new LsbReader(data, data.Length, forward: false);
        Span<int> states = stackalloc int[5];
        states[0] = ReadState(ref front, tableBits); states[1] = ReadState(ref back, tableBits);
        states[2] = ReadState(ref front, tableBits); states[3] = ReadState(ref back, tableBits);
        states[4] = ReadState(ref front, tableBits);
        var result = new byte[real];
        for (var i = 0; i < real - 5; i++)
        {
            var k = i % 5;
            var state = states[k];
            result[i] = symbols[state];
            int n = bits[state], value;
            if (((i / 5) & 1) == 0) { value = (int)(front.Peek() & ((1u << n) - 1)); front.Skip(n); }
            else { value = (int)(back.Peek() & ((1u << n) - 1)); back.Skip(n); }
            states[k] = bases[state] + value;
        }
        for (var k = 0; k < 5; k++) result[real - 5 + k] = (byte)states[k];
        return front.Overrun || back.Overrun ? null : result;

        static int ReadState(ref LsbReader reader, int n)
        {
            var v = (int)(reader.Peek() & ((1u << n) - 1));
            reader.Skip(n);
            return v;
        }
    }

    // ---- Huffman ----

    /// <summary>
    /// Types 2 and 4. A header with the code lengths (<see cref="ReadHuffmanTable"/>), then one group of three streams (type 2), or two
    /// groups for the two halves of the output (type 4, preceded by the size of the first as 3 bytes). A group is a 16 bit size of its first
    /// stream, the first stream, the second stream, and the third stream read backwards from the end of the group. Symbol i of a group comes
    /// from stream i mod 3 (first, third, second); bits are taken least significant first and a code's first bit is its most significant.
    /// </summary>
    static byte[]? DecodeHuffman(ReadOnlySpan<byte> src, int real, bool twoGroups)
    {
        var table = ReadHuffmanTable(src, out var pos);
        if (table is null) return null;
        var result = new byte[real];
        if (!twoGroups) return DecodeGroup(src[pos..], table, result) ? result : null;
        if (src.Length - pos < 3) return null;
        var first = src[pos] | src[pos + 1] << 8 | src[pos + 2] << 16;
        pos += 3;
        if (src.Length - pos < first) return null;
        var half = (real + 1) >> 1;
        return DecodeGroup(src.Slice(pos, first), table, result.AsSpan(0, half)) && DecodeGroup(src[(pos + first)..], table, result.AsSpan(half)) ? result : null;
    }

    sealed class HuffmanTable(ushort[] lut, int bits)
    {
        public readonly ushort[] Lut = lut;   // indexed by the next Bits bits (first bit read is bit 0): symbol << 4 | length, 0 = invalid
        public readonly int Bits = bits;
    }

    /// <summary>
    /// Header: bits 1, 0 = new style (0 = old style, see below), then 2 bits "forced bits" k, 8 bits symbol count - 1, a truncated binary
    /// number F, then count + F unary numbers (zeros before a 1). The first count give the code lengths: each is extended with k raw bits and
    /// is a zigzag delta of a running sum that starts at 30 (length = sum / 4 + 1 + delta). The other F numbers describe which symbols are
    /// present as alternating runs and gaps (the first entry alone is a gap if F is odd), their extra bits following the raw bits. Codes are
    /// canonical: shorter first, then by symbol.
    /// </summary>
    static HuffmanTable? ReadHuffmanTable(ReadOnlySpan<byte> src, out int bytesUsed)
    {
        bytesUsed = 0;
        var r = new MsbReader(src);
        int[] lengths;
        int[] symbols;
        var count = 0;
        if (r.Read(1) == 0)
        {
            // Old style, in two forms. Lengths are zigzag deltas around a running average of the previous ones.
            var codes = new List<(int Symbol, int Length)>();
            if (r.Read(1) == 1)
            {
                // Runs: alternating gaps (symbols absent) and groups (symbols present, one length each), sizes as Elias gamma numbers.
                var forced = r.Read(2);
                int symbol = 0, average = 32;
                var skipGap = r.Read(1) == 1;
                while (symbol < 256)
                {
                    if (!skipGap) symbol += ReadWideGamma(ref r) - 1;
                    skipGap = false;
                    if (symbol >= 256) break;
                    var n = ReadWideGamma(ref r) - 1;
                    if (symbol + n > 256) return null;
                    for (; n > 0; n--, symbol++)
                    {
                        var v = r.ReadUnary() << forced | r.Read(forced);
                        var length = ((v >> 1) ^ -(v & 1)) + ((average + 2) >> 2);
                        if (length is < 1 or > MaxCodeLength || r.Overrun) return null;
                        average = length + ((3 * average + 2) >> 2);
                        codes.Add((symbol, length));
                    }
                }
            }
            else
            {
                // A list of (symbol, length) pairs; a single symbol is not handled (the encoder uses run-length blocks for that).
                var n = r.Read(8);
                if (n < 2) return null;
                var lengthBits = r.Read(3);
                if (lengthBits > 4) return null;
                for (var i = 0; i < n; i++) codes.Add((r.Read(8), r.Read(lengthBits) + 1));
                if (codes.Any(c => c.Length > MaxCodeLength) || codes.Select(c => c.Symbol).Distinct().Count() != n) return null;
            }
            if (codes.Count < 2) return null;
            count = codes.Count;
            lengths = codes.Select(c => c.Length).ToArray();
            symbols = codes.Select(c => c.Symbol).ToArray();
        }
        else
        {
            if (r.Read(1) != 0) return null;
            var forced = r.Read(2);
            count = r.Read(8) + 1;
            var fluff = ReadFluff(ref r, count);
            var unary = new int[count + fluff];
            for (var i = 0; i < unary.Length; i++) unary[i] = r.ReadUnary();
            lengths = new int[count];
            var sum = 30;
            for (var i = 0; i < count; i++)
            {
                var value = unary[i] << forced | r.Read(forced);
                var delta = (value >> 1) ^ -(value & 1);
                lengths[i] = (sum >> 2) + 1 + delta;
                sum += delta;
                if (lengths[i] is < 1 or > MaxCodeLength) return null;
            }
            symbols = ReadSymbolRanges(ref r, unary, count, fluff)!;
            if (symbols is null) return null;
        }
        bytesUsed = (r.Position + 7) >> 3;
        if (r.Overrun) return null;

        // Canonical codes: shorter first, then by symbol; the LUT is indexed by the bit reversed code.
        var order = Enumerable.Range(0, count).OrderBy(i => lengths[i]).ThenBy(i => i).ToArray();
        var max = lengths.Max();
        var lut = new ushort[1 << max];
        var code = 0;
        var prev = 0;
        foreach (var i in order)
        {
            code <<= lengths[i] - prev;
            prev = lengths[i];
            if (code >= 1 << prev) return null;
            var reversed = Reverse(code, prev);
            for (var fill = reversed; fill < lut.Length; fill += 1 << prev) lut[fill] = (ushort)(symbols[i] << 4 | prev);
            code++;
        }
        return new HuffmanTable(lut, max);

    }

    /// <summary>The number of extra unary numbers that describe which symbols are present, in a truncated binary code (none when all 256 are).</summary>
    static int ReadFluff(ref MsbReader r, int count)
    {
        if (count == 256) return 0;
        var x = Math.Min(257 - count, count) * 2;
        var y = BitOperations.Log2((uint)(x - 1)) + 1;
        var v = r.Read(y);
        var z = (1 << y) - x;
        if ((v >> 1) >= z) return v - z;
        r.Unread(1);
        return v >> 1;
    }

    /// <summary>
    /// The symbols present, ascending. The fluff unary numbers after the first <paramref name="count"/> give the sizes of alternating runs of
    /// present symbols and gaps of absent ones (a lone gap first if the fluff is odd); what is left after the last gap is present.
    /// </summary>
    static int[]? ReadSymbolRanges(ref MsbReader r, int[] unary, int count, int fluff)
    {
        var symbols = new int[count];
        int n = 0, at = 0, k = 0;
        if ((fluff & 1) != 0) { at = ReadGap(ref r, unary[count]); k = 1; }
        for (; k < fluff; k += 2)
        {
            var run = ReadRun(ref r, unary[count + k]);
            var gap = ReadGap(ref r, unary[count + k + 1]);
            if (n + run > count || at + run > 256) return null;
            for (var j = 0; j < run; j++) symbols[n++] = at++;
            at += gap;
        }
        if (at + (count - n) > 256) return null;
        while (n < count) symbols[n++] = at++;
        return symbols;

        static int ReadRun(ref MsbReader r, int q) => 1 << q | r.Read(q);
        static int ReadGap(ref MsbReader r, int q) => (1 << (q + 1)) - 1 + r.Read(q + 1);
    }

    static int Reverse(int code, int length)
    {
        var r = 0;
        for (var i = 0; i < length; i++) r |= ((code >> i) & 1) << (length - 1 - i);
        return r;
    }

    static bool DecodeGroup(ReadOnlySpan<byte> group, HuffmanTable table, Span<byte> output)
    {
        if (group.Length < 2) return output.Length == 0;
        var first = group[0] | group[1] << 8;
        if (group.Length - 2 < first) return false;
        var a = new LsbReader(group, 2, forward: true);
        var b = new LsbReader(group, group.Length, forward: false);
        var c = new LsbReader(group, 2 + first, forward: true);
        var lut = table.Lut;
        var mask = (1 << table.Bits) - 1;
        for (var i = 0; i < output.Length; i++)
        {
            ref var reader = ref (i % 3 == 0 ? ref a : ref (i % 3 == 1 ? ref b : ref c));
            var entry = lut[reader.Peek() & mask];
            if (entry == 0) return false;
            reader.Skip(entry & 15);
            output[i] = (byte)(entry >> 4);
        }
        return !a.Overrun && !b.Overrun && !c.Overrun;
    }

    // ---- LZ ----

    /// <summary>
    /// An LZ chunk of 128 KB or less: [8 bytes copied as they are, for the first chunk of the file], literals, commands, offsets (with
    /// an optional scale), lengths, and a tail with the bits that complete the offsets and the long lengths. A command byte is
    /// [offset index:2][match length - 2:4][literal count:2]: index 3 takes the next offset, 0 to 2 pick one of the last three used (the pick
    /// moves to the front); a literal count of 3 or a match length code of 15 adds a value from the length stream (255 = continued in the
    /// tail). Literals are plain (mode 1) or added to the byte one recent offset back (mode 0). After the last command the remaining
    /// literals follow.
    /// </summary>
    static bool DecodeLzChunk(ReadOnlySpan<byte> src, Span<byte> dst, int start, int count, int mode)
    {
        var sp = 0;
        var o = start;
        var end = start + count;
        if (start == 0)
        {
            if (src.Length < 8) return false;
            src[..8].CopyTo(dst);
            sp = 8; o = 8;
        }
        var lit = DecodeBytes(src, ref sp, count);
        var cmd = DecodeBytes(src, ref sp, count);
        if (lit is null || cmd is null || sp >= src.Length) return false;
        var scale = 0;
        if ((src[sp] & 0x80) != 0) scale = src[sp++] - 127;
        var offsets = DecodeBytes(src, ref sp, count);
        var extra = scale > 1 ? DecodeBytes(src, ref sp, count) : null;
        var lengths = DecodeBytes(src, ref sp, count);
        if (offsets is null || lengths is null || (scale > 1 && extra is null)) return false;
        var tail = src[sp..];

        var fwd = new MsbReader(tail);
        var bwd = new MsbReader(tail, backwards: true);
        var excessCount = ReadGamma(ref bwd) - 1;
        var distances = new int[offsets.Length];
        for (var i = 0; i < distances.Length; i++)
        {
            ref var r = ref (i % 2 == 0 ? ref fwd : ref bwd);
            var code = offsets[i];
            if (scale == 0)
            {
                if (code >= 0xF0)
                {
                    // Far distances: 4 to 19 bits (after the leading 1) for the high part, then 12 for the rest.
                    var far = code - 0xF0 + 4;
                    var high = (long)((1 << far) | r.Read(far));
                    var farDistance = 8322816 + (high << 12) + r.Read(12);
                    if (farDistance > int.MaxValue) return false;
                    distances[i] = (int)farDistance;
                    continue;
                }
                var n = (code >> 4) + 4;
                distances[i] = (((1 << n) | r.Read(n)) << 4 | (code & 15)) - 248;
            }
            else
            {
                var n = code >> 3;
                var value = (((8 + (code & 7)) << n) | r.Read(n)) - 8;
                distances[i] = scale == 1 ? value : scale * value + extra![i];
            }
        }
        var excess = new int[excessCount];
        for (var i = 0; i < excess.Length; i++)
        {
            ref var r = ref (i % 2 == 0 ? ref fwd : ref bwd);
            excess[i] = ReadLength(ref r);
        }
        if (fwd.Overrun || bwd.Overrun) return false;

        int li = 0, ni = 0, ei = 0, oi = 0;
        Span<int> recent = [8, 8, 8];
        int NextLength()
        {
            var b = lengths[ni++];
            return b == 255 ? 255 + excess[ei++] : b;
        }
        try
        {
            foreach (var c in cmd)
            {
                var literals = c & 3;
                var match = ((c >> 2) & 0xF) + 2;
                if (literals == 3) literals += NextLength();
                if (match == 17) match += NextLength();
                if (literals > end - o || literals > lit.Length - li) return false;
                if (mode == 0) for (var i = 0; i < literals; i++) { dst[o] = (byte)(lit[li++] + dst[o - recent[0]]); o++; }
                else { lit.AsSpan(li, literals).CopyTo(dst[o..]); li += literals; o += literals; }

                int distance;
                var index = c >> 6;
                if (index == 3) { distance = distances[oi++]; recent[2] = recent[1]; recent[1] = recent[0]; recent[0] = distance; }
                else
                {
                    distance = recent[index];
                    for (var i = index; i > 0; i--) recent[i] = recent[i - 1];
                    recent[0] = distance;
                }
                if (distance <= 0 || distance > o || match > end - o) return false;
                for (var i = 0; i < match; i++, o++) dst[o] = dst[o - distance];
            }
            var rest = end - o;
            if (rest != lit.Length - li) return false;
            if (mode == 0) for (var i = 0; i < rest; i++) { dst[o] = (byte)(lit[li++] + dst[o - recent[0]]); o++; }
            else { lit.AsSpan(li, rest).CopyTo(dst[o..]); o += rest; }
        }
        catch (IndexOutOfRangeException) { return false; }
        return ni == lengths.Length && ei == excess.Length && oi == distances.Length;
    }

    /// <summary>Zeros then a 1 then as many more bits as there were zeros: the value as written, at least 1.</summary>
    /// <summary>The old Huffman header's counts: zeros, a 1, then one more bit than there were zeros; the value includes the leading 1 bit.</summary>
    static int ReadWideGamma(ref MsbReader r)
    {
        var zeros = r.ReadUnary() + 1;
        return 1 << zeros | r.Read(zeros);
    }

    static int ReadGamma(ref MsbReader r)
    {
        var zeros = r.ReadUnary();
        return 1 << zeros | r.Read(zeros);
    }

    /// <summary>A long length: zeros, then 6 + zeros + 1 bits starting with the 1, minus 64.</summary>
    static int ReadLength(ref MsbReader r)
    {
        var zeros = r.ReadUnary();
        return ((1 << (zeros + 6)) | r.Read(zeros + 6)) - 64;
    }

    // ---- bit readers ----

    /// <summary>Most significant bit first, from the start of the data, or from the last byte going backwards.</summary>
    ref struct MsbReader
    {
        readonly ReadOnlySpan<byte> data;
        readonly bool backwards;
        int bit;   // bits consumed
        public bool Overrun { get; private set; }
        public int Position => bit;

        public MsbReader(ReadOnlySpan<byte> data, bool backwards = false) { this.data = data; this.backwards = backwards; }

        int Next()
        {
            var index = bit >> 3;
            if (index >= data.Length) { Overrun = true; bit++; return 0; }
            var b = data[backwards ? data.Length - 1 - index : index];
            return (b >> (7 - (bit++ & 7))) & 1;
        }

        public int Read(int n)
        {
            var v = 0;
            for (var i = 0; i < n; i++) v = v << 1 | Next();
            return v;
        }

        public void Unread(int n) => bit -= n;

        public int ReadUnary()
        {
            var zeros = 0;
            while (Next() == 0) { zeros++; if (Overrun) break; }
            return zeros;
        }
    }

    /// <summary>Least significant bit first, bytes in order or in reverse order; keeps 64 bits ahead.</summary>
    ref struct LsbReader
    {
        readonly ReadOnlySpan<byte> data;
        readonly bool forward;
        int next;          // index of the next byte to load
        ulong bits;
        int available;     // bits in the register
        long real;         // bits that came from real bytes
        long consumed;

        public LsbReader(ReadOnlySpan<byte> data, int start, bool forward)
        {
            this.data = data; this.forward = forward;
            next = forward ? start : start - 1;
            Refill();
        }

        public bool Overrun => consumed > real;

        void Refill()
        {
            while (available <= 56)
            {
                ulong b = 0;
                if (forward ? next < data.Length : next >= 0)
                {
                    b = data[next];
                    next += forward ? 1 : -1;
                    real += 8;
                }
                bits |= b << available;
                available += 8;
            }
        }

        public uint Peek() => (uint)bits;

        public void Skip(int n)
        {
            bits >>= n;
            available -= n;
            consumed += n;
            if (available < 32) Refill();
        }
    }
}
