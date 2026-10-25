using RelicWriter.Common;
using System.Runtime.InteropServices;

namespace RelicWriter.GameAssets;

/// <summary>
/// Decompression for the game's archives. On Windows it uses the Oodle library that ships with the game (bin/x64/oo2ext_7_win64.dll);
/// nothing from Oodle is bundled with RelicWriter. That DLL is a Windows library, so everywhere else (and on Windows when the DLL is
/// missing) the managed <see cref="Kraken"/> decoder is used.
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
        if (OperatingSystem.IsWindows() && File.Exists(path) && NativeLibrary.TryLoad(path, out var lib))
            NativeLibrary.TryGetExport(lib, "OodleLZ_Decompress", out decompress);
        return true;
    }

    /// <summary>Decompresses exactly <paramref name="dst"/>.Length bytes; false when Oodle reports an error.</summary>
    public static bool Decompress(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        if (decompress == 0) return Kraken.TryDecompress(src, dst);
        fixed (byte* s = src)
        fixed (byte* d = dst)
        {
                        var n = ((delegate* unmanaged[Cdecl]<byte*, long, byte*, long, int, int, int, void*, long, void*, void*, void*, long, int, long>)decompress)(
                s, src.Length, d, dst.Length, FuzzSafe, NoCrcCheck, Quiet, null, 0, null, null, null, 0, ThreadPhaseAll);
            return n == dst.Length;
        }
    }
}
