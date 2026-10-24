using RelicWriter.Common;
using System.Runtime.InteropServices;

namespace RelicWriter.GameAssets;

/// <summary>
/// Decompression for the game's archives, using the Oodle library that ships with the game (bin/x64/oo2ext_7_win64.dll).
/// Nothing from Oodle is bundled with RelicWriter. The DLL is a Windows library, so this works on Windows only;
/// elsewhere <see cref="TryLoad"/> returns false and the compressed archive files cannot be read.
/// </summary>
internal static unsafe class Oodle
{
    static nint decompress;

    // OodleLZ_Decompress arguments after the buffers: fuzzSafe, checkCRC, verbosity, then (unused here) decBufBase/Size, callback, userData, decoderMem/Size, and threadPhase.
    const int FuzzSafe = 1, NoCrcCheck = 0, Quiet = 0, ThreadPhaseAll = 3;

    public static bool TryLoad(string gameDir)
    {
        if (decompress != 0) return true;
        var path = Path.Combine(gameDir, "bin", "x64", "oo2ext_7_win64.dll");
        if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out var lib)) return false;
        return NativeLibrary.TryGetExport(lib, "OodleLZ_Decompress", out decompress);
    }

    /// <summary>Decompresses exactly <paramref name="dst"/>.Length bytes; false when Oodle reports an error.</summary>
    public static bool Decompress(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        if (decompress == 0) return false;
        fixed (byte* s = src)
        fixed (byte* d = dst)
        {
                        var n = ((delegate* unmanaged[Cdecl]<byte*, long, byte*, long, int, int, int, void*, long, void*, void*, void*, long, int, long>)decompress)(
                s, src.Length, d, dst.Length, FuzzSafe, NoCrcCheck, Quiet, null, 0, null, null, null, 0, ThreadPhaseAll);
            return n == dst.Length;
        }
    }
}
