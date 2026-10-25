namespace RelicWriter.Common.Container;

/// <param name="Prefix">Bytes 0..24 of the file, kept verbatim: magic, versions, game definition, clock, date, archive version.</param>
/// <param name="ChunkTableCapacity">Number of chunk table slots in the file (256 or 512 in known saves); the first chunk starts right after them.</param>
public sealed record SaveHeader(uint SaveVersion, uint GameVersion, uint ArchiveVersion, byte[] Prefix, int ChunkTableCapacity)
{
    /// <summary>Offsets in <see cref="Prefix"/> of the packed clock and date.</summary>
    const int ClockOffset = 13, DateOffset = 17;

    // The clock is hour, minute, second and millisecond packed into one u32 (see docs/save-format.md).
    const int HourShift = 22, MinuteShift = 16, SecondShift = 10, TimeFieldMask = 63;

    // The date packs year, month and day; month and day are stored from 0.
    const int YearShift = 20, MonthShift = 15, DayShift = 10, DateFieldModulo = 32;

    /// <summary>Game versions below this are builds before patch 1.1, which have no patch number; from here a version is major * 1000 + minor * 10.</summary>
    const uint FirstPatchNumberedVersion = 1000;
    const uint MajorDivisor = 1000, MinorDivisor = 10;

    /// <summary>The same header with another game version, in the number and in the bytes the writer copies. The save version is left alone.</summary>
    public SaveHeader WithGameVersion(uint gameVersion)
    {
        var prefix = (byte[])Prefix.Clone();
        BitConverter.GetBytes(gameVersion).CopyTo(prefix, SaveLayout.GameVersionOffset);
        return this with { GameVersion = gameVersion, Prefix = prefix };
    }

    /// <summary>When the save was written (local time as the game stored it), from the packed clock and date.</summary>
    public DateTime? Timestamp
    {
        get
        {
            var clock = BitConverter.ToUInt32(Prefix, ClockOffset);
            var date = BitConverter.ToUInt32(Prefix, DateOffset);
            try
            {
                return new DateTime((int)(date >> YearShift), 1 + (int)((date >> MonthShift) % DateFieldModulo), 1 + (int)((date >> DayShift) % DateFieldModulo),
                    (int)(clock >> HourShift), (int)((clock >> MinuteShift) & TimeFieldMask), (int)((clock >> SecondShift) & TimeFieldMask));
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }

    /// <summary>The game patch the save was written by, from the game version number (2310 is 2.31; values below 1000 are pre-1.1 builds).</summary>
    public string GameVersionLabel
    {
        get
        {
            if (GameVersion < FirstPatchNumberedVersion) return $"build {GameVersion} (before 1.1)";
            var minor = (GameVersion % MajorDivisor / MinorDivisor).ToString().TrimEnd('0');
            return $"{GameVersion / MajorDivisor}.{(minor.Length == 0 ? "0" : minor)}";
        }
    }
}
