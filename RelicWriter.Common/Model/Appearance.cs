using RelicWriter.Common.Codecs;
using RelicWriter.Common.Container;

namespace RelicWriter.Common.Model;

/// <summary>One mesh/appearance resource of the character, with the tag the game files it under (for example eyes_color or hair_color50).</summary>
public sealed class AppearancePart
{
    /// <summary>From archive version 195 a hash (the hash function is not identified); before that <see cref="NameText"/> is used.</summary>
    public ulong Hash { get; set; }
    public string NameText { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Tag { get; set; } = "";
    public uint Unknown1 { get; set; }
    public uint Unknown2 { get; set; }
}

/// <summary>A named character-creator slot and the preset picked for it, for example eyes = h041.</summary>
public sealed class AppearanceChoice
{
    public string Slot { get; set; } = "";
    public string Value { get; set; } = "";
    public uint C { get; set; }
    public uint D { get; set; }
}

public sealed class AppearanceGroup
{
    public string Name { get; set; } = "";
    public List<AppearancePart> Parts { get; } = [];
    public List<AppearanceChoice> Choices { get; } = [];
}

/// <summary>
/// The CharacetrCustomization_Appearances node (the misspelling is the game's): how V looks. Layout in docs/save-spec.xml.
/// Read returns null unless the node re-encodes to exactly the bytes it was read from, so a save is never rewritten from a misread node.
/// </summary>
public sealed class Appearance
{
    public const string NodeName = "CharacetrCustomization_Appearances";
    /// <summary>Choice values look like h041: an 'h' and a three-digit preset number.</summary>
    public const char PresetPrefix = 'h';
    const int PresetDigits = 3;
    /// <summary>Part names became hashes at this archive version; the part tag exists from this save version; the trailing name list from the next one after 171.</summary>
    const uint HashedPartNames = 195, PartTag = 168, NameListAfter = 171;

    readonly SaveNode node;
    readonly SaveContext ctx;
    bool changed;

    bool dataExists;
    uint unknown1, unknown2;
    byte unknown3, unknown4;
    readonly List<(string Base, string FirstPerson, string ThirdPerson)> mappings = [];
    readonly List<string> names = [];

    Appearance(SaveNode node, SaveContext ctx)
    {
        this.node = node;
        this.ctx = ctx;
    }

    public List<AppearanceGroup> CharacterGroups { get; } = [];
    public List<AppearanceGroup> WeaponGroups { get; } = [];
    public List<AppearanceGroup> BodyGroups { get; } = [];

    /// <summary>The creator slots that hold a preset number (eyes, nose, mouth, jaw), in the order they first appear.</summary>
    public IEnumerable<string> PresetSlots => AllGroups.SelectMany(g => g.Choices).Where(c => ParsePreset(c.Value) is not null).Select(c => c.Slot).Distinct();

    IEnumerable<AppearanceGroup> AllGroups => CharacterGroups.Concat(WeaponGroups).Concat(BodyGroups);

    /// <summary>The preset number picked for a slot, or null.</summary>
    public int? Preset(string slot) => AllGroups.SelectMany(g => g.Choices).Where(c => c.Slot == slot).Select(c => ParsePreset(c.Value)).FirstOrDefault(p => p is not null);

    /// <summary>Sets the preset number in every group that has the slot (the game keeps the same choice in several groups).</summary>
    public void SetPreset(string slot, int number)
    {
        var value = PresetPrefix + number.ToString("D" + PresetDigits);
        foreach (var c in AllGroups.SelectMany(g => g.Choices).Where(c => c.Slot == slot && c.Value != value)) { c.Value = value; changed = true; }
    }

    /// <summary>The parts of the group the character creator fills (skin, hair, eyes, tattoos, makeup, cyberware), as tag and resource.</summary>
    public IEnumerable<AppearancePart> CreatorParts => CharacterGroups.FirstOrDefault(g => g.Name == "character_customization")?.Parts ?? [];

    static int? ParsePreset(string value) =>
        value.Length == 1 + PresetDigits && value[0] == PresetPrefix && int.TryParse(value.AsSpan(1), out var n) ? n : null;

    public static Appearance? Read(SaveFile file)
    {
        var node = file.Walk().FirstOrDefault(n => n.Name == NodeName);
        if (node is null) return null;
        var ctx = new SaveContext(file.Header.SaveVersion, file.Header.GameVersion, file.Header.ArchiveVersion);
        try
        {
            var appearance = new Appearance(node, ctx);
            appearance.Parse();
            return appearance.Encode().AsSpan().SequenceEqual(node.Data) ? appearance : null;
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidDataException or OverflowException) { return null; }
    }

    /// <summary>Writes the node back. Does nothing when nothing changed, so an unedited save stays byte-identical.</summary>
    public void ApplyTo()
    {
        if (!changed) return;
        node.Data = Encode();
        changed = false;
    }

    // ---- decoding ----

    void Parse()
    {
        var r = new NodeReader(node.Data);
        dataExists = r.U8() != 0;
        unknown1 = r.U32();
        if (dataExists)
        {
            unknown2 = r.U32();
            unknown3 = r.U8();
            unknown4 = r.U8();
            ReadGroups(ref r, CharacterGroups);
            ReadGroups(ref r, WeaponGroups);
            ReadGroups(ref r, BodyGroups);
            for (var i = r.U32(); i > 0; i--) mappings.Add((r.String(), r.String(), r.String()));
            if (ctx.SaveVersion > NameListAfter)
                for (var i = r.Varint(); i > 0; i--) names.Add(r.String());
        }
        if (r.Remaining != 0) throw new InvalidDataException($"{NodeName}: {r.Remaining} bytes left after reading");
    }

    void ReadGroups(ref NodeReader r, List<AppearanceGroup> groups)
    {
        for (var g = r.U32(); g > 0; g--)
        {
            var group = new AppearanceGroup { Name = r.String() };
            for (var p = r.U32(); p > 0; p--)
            {
                var part = new AppearancePart();
                if (ctx.ArchiveVersion >= HashedPartNames) part.Hash = r.U64(); else part.NameText = r.String();
                part.Resource = r.String();
                if (ctx.SaveVersion >= PartTag) part.Tag = r.String();
                part.Unknown1 = r.U32();
                part.Unknown2 = r.U32();
                group.Parts.Add(part);
            }
            for (var c = r.U32(); c > 0; c--) group.Choices.Add(new AppearanceChoice { Slot = r.String(), Value = r.String(), C = r.U32(), D = r.U32() });
            groups.Add(group);
        }
    }

    // ---- encoding ----

    byte[] Encode()
    {
        var w = new NodeWriter();
        w.U8(dataExists ? (byte)1 : (byte)0);
        w.U32(unknown1);
        if (dataExists)
        {
            w.U32(unknown2);
            w.U8(unknown3);
            w.U8(unknown4);
            WriteGroups(w, CharacterGroups);
            WriteGroups(w, WeaponGroups);
            WriteGroups(w, BodyGroups);
            w.U32((uint)mappings.Count);
            foreach (var (b, f, t) in mappings) { w.String(b); w.String(f); w.String(t); }
            if (ctx.SaveVersion > NameListAfter)
            {
                w.Varint(names.Count);
                foreach (var n in names) w.String(n);
            }
        }
        return w.ToArray();
    }

    void WriteGroups(NodeWriter w, List<AppearanceGroup> groups)
    {
        w.U32((uint)groups.Count);
        foreach (var group in groups)
        {
            w.String(group.Name);
            w.U32((uint)group.Parts.Count);
            foreach (var part in group.Parts)
            {
                if (ctx.ArchiveVersion >= HashedPartNames) w.U64(part.Hash); else w.String(part.NameText);
                w.String(part.Resource);
                if (ctx.SaveVersion >= PartTag) w.String(part.Tag);
                w.U32(part.Unknown1);
                w.U32(part.Unknown2);
            }
            w.U32((uint)group.Choices.Count);
            foreach (var c in group.Choices) { w.String(c.Slot); w.String(c.Value); w.U32(c.C); w.U32(c.D); }
        }
    }
}
