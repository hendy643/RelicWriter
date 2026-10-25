# Kraken (Oodle) decoder

The game's archives compress their segments with Oodle Kraken. `RelicWriter.GameAssets/Kraken.cs` is a managed decoder, so reading the game files works on every platform; on Windows the game's own DLL is still used when it is present (`Oodle.cs`). The decoder was written from the bitstream itself: the format was worked out by compressing synthetic data with the game's own `OodleLZ_Compress` (run under Wine) and reading what came out, and checked byte for byte against `OodleLZ_Decompress` on 360 real archive segments and on the 16 MB vehicle atlas texture. Three details that could not be worked out in reasonable time were read from [oozextract](https://github.com/lvlvllvlvllvlvl/oozextract) (a Rust port of ooz; it declares MIT in `Cargo.toml` but ships no licence file, and ooz itself has none) and written again here in this file's own structure: the sparse tANS weight mapping, the old Huffman header, and the far match distances. Check that provenance before a release if it matters. `RelicWriter.Tests/KrakenTests.cs` holds small streams made that way from generated data.

## Status

Understood and tested: stored blocks (bit 6 of the block header), memset blocks, LZ chunks (all offset forms, including far distances), Huffman (type 2 and 4, both headers), run length (type 3), sequences of blocks (type 5), tANS with a dense or a sparse table. All 360 sampled segments and the vehicle atlas decode exactly, so the vehicle pictures work on Linux.

Not done (the decoder returns false, like for a corrupt file):

- Decoder types other than Kraken (6), the "several arrays" form of type 5, an old-style Huffman header with a single symbol, and block header bit 5 (its meaning is not known).

## Layout

A segment (after the archive's 8 byte `KARK` header) is cut in 256 KB blocks. Each starts with 2 bytes: low nibble 0xC, bit 5 of the first = stored, second byte & 0x7F = 6. Then a 3 byte big-endian quantum header: the low 18 bits + 1 are the packed size (equal to the block size = stored; 0x3FFFF with bit 18 set = one byte repeated, the byte follows). The packed data is up to two 128 KB chunks. A chunk starts with 3 bytes: bit 23 set = LZ chunk (low 19 bits = size, bits 19..22 = mode: 1 plain literals, 0 literals added to the byte at the last offset), bit 23 clear = one entropy block of the chunk size.

An entropy block starts with 2/3/5 bytes: type in bits 4..6 of the first byte; with bit 7 set a 3 byte form (10 bit packed size, 10 bit size difference), else 5 bytes (18 bit packed size, 18 bit size - 1); type 0 has its own 2 and 3 byte forms.

LZ chunk: (8 raw bytes for the first chunk of the file), literals, commands, an offset flag byte (bit 7 set: scale = byte - 127; a scale above 1 adds a second stream of offset remainders), offsets, lengths, and the tail. The tail is two bit streams read most significant bit first, one from the front and one backwards from the last byte; the first (backwards) holds a count of long lengths as zeros, a 1, and as many more bits as zeros. Then the offsets alternate between the two streams: code c without scale takes n = c >> 4 + 4 bits b and gives ((1 << n | b) << 4 | c & 15) - 248; with a scale n = c >> 3 and ((8 + c & 7) << n | b) - 8, times the scale plus the remainder. Then the long lengths: zeros, then 6 + zeros + 1 bits minus 64. See the comments in `Kraken.cs` for the command byte and the Huffman and tANS details.
