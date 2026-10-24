using System.Text;
using RelicWriter.Common.Container;
using RelicWriter.Common.Formats;
using RelicWriter.Common.Model;

namespace RelicWriter.Tests.Fixtures;

/// <summary>
/// Builds small saves in memory, laid out the way the game writes them (see docs/save-spec.xml), so the tests need no real save files.
/// Each save has a character (a ScriptableSystemsContainer blob), an inventory with two sub-inventories, and, from save version 205, a garage.
/// </summary>
static class SyntheticSave
{
    /// <summary>Save versions the tests run on, with the game version each shipped with.</summary>
    public static readonly (uint Save, uint Game)[] Versions = [(193, 8), (224, 1600), (254, 2010), (261, 2100), (263, 2120), (269, 2310), (277, 2500)];

    public static IEnumerable<object[]> AllVersions() => Versions.Select(v => new object[] { v.Save });

    public static readonly string[] OwnedVehicles = ["Vehicle.v_sport2_quadra_type66_player", "Vehicle.v_standard25_mahir_supron_player"];

    /// <summary>The file as the game would have written it.</summary>
    public static byte[] Bytes(uint version) => Create(version).Write();

    /// <summary>Archive version of the fixture saves; a test can ask for 195 to get hashed appearance part names.</summary>
    public const uint DefaultArchive = 2, HashedPartArchive = 195;

    public static SaveFile Read(uint version) => SaveFile.Read(Bytes(version));

    public static SaveFile Create(uint version, uint archive = DefaultArchive)
    {
        var game = Versions.First(v => v.Save == version).Game;
        var prefix = new byte[25];
        Encoding.ASCII.GetBytes("VASC").CopyTo(prefix, 0);
        BitConverter.GetBytes(version).CopyTo(prefix, 4);
        BitConverter.GetBytes(game).CopyTo(prefix, 8);
        BitConverter.GetBytes(archive).CopyTo(prefix, 21);
        var save = new SaveFile { Header = new SaveHeader(version, game, archive, prefix, 256) };

        save.Roots.Add(new SaveNode { Name = "ScriptableSystemsContainer", Id = 1, Data = Character(version).Write() });
        save.Roots.Add(Inventory(version));
        save.Roots.Add(new SaveNode { Name = "CharacetrCustomization_Appearances", Id = 7, Data = Appearance(version, archive) });
        if (version >= 205) save.Roots.Add(new SaveNode { Name = "PersistencySystem2", Id = 2, Data = Persistency() });
        // A node with data after its last child, like TimeSystem in the game.
        var time = new SaveNode { Name = "TimeSystem", Id = 3, Data = [1, 2, 3, 4], Trailing = new byte[17] };
        time.Children.Add(new SaveNode { Name = "TimeChild", Id = 6, Data = [5] });
        save.Roots.Add(time);
        return save;
    }

    // ---- character: PlayerDevelopmentData inside a system blob ----

    static SystemBlob Character(uint version)
    {
        var blob = new SystemBlob();
        BlobStruct New() => new(blob);
        void Field(BlobStruct s, string name, string type, byte[] data) => s.Fields.Add(new BlobField(blob.Intern(name), blob.Intern(type), data));
        byte[] Int(int x) => BitConverter.GetBytes(x);
        byte[] Enum(string value) => BitConverter.GetBytes((ushort)blob.Intern(value));
        byte[] Array(IEnumerable<BlobStruct> items)
        {
            var list = items.ToList();
            var o = new MemoryStream();
            o.Write(BitConverter.GetBytes((uint)list.Count));
            foreach (var i in list) o.Write(i.ToBytes());
            return o.ToArray();
        }
        BlobStruct Pair(string enumField, string enumType, string value, string intField, int n)
        {
            var s = New();
            Field(s, enumField, enumType, Enum(value));
            Field(s, intField, "Int32", Int(n));
            return s;
        }

        var body = New();
        var owner = New();
        Field(owner, "hash", "Uint64", BitConverter.GetBytes(1UL));
        Field(body, "ownerID", "entEntityID", owner.ToBytes());
        Field(body, "attributes", "array:SAttribute", Array(new[] { "Strength", "Reflexes", "TechnicalAbility", "Intelligence", "Cool" }
            .Select((a, i) => Pair("attributeName", "gamedataStatType", a, "value", 10 + i))));
        Field(body, "proficiencies", "array:SProficiency", Array(new[] { ("Level", 30), ("StreetCred", 20), ("Assault", 5), ("Hacking", 7), ("Athletics", 3) }
            .Select(p => Pair("type", "gamedataProficiencyType", p.Item1, "currentLevel", p.Item2))));
        Field(body, "devPoints", "array:SDevelopmentPoints", Array(new[] { ("Attribute", 2), ("Primary", 4), ("Secondary", 6) }
            .Select(p => Pair("type", "gamedataDevelopmentPointType", p.Item1, "unspent", p.Item2))));
        if (version >= 254)
        {
            // Phantom Liberty: the Relic tree is an attribute-data entry of its own; two perks owned.
            var relic = New();
            Field(relic, "type", "gamedataAttributeDataType", Enum("EspionageAttributeData"));
            Field(relic, "unlockedPerks", "array:SNewPerk", Array(new[] { "Espionage_Central_Milestone_1", "Espionage_Central_Perk_1_1" }
                .Select(p => Pair("type", "gamedataNewPerkType", p, "currLevel", 1))));
            // One regular tree too, with a single perk bought.
            var cool = New();
            Field(cool, "type", "gamedataAttributeDataType", Enum("CoolAttributeData"));
            Field(cool, "unlockedPerks", "array:SNewPerk", Array([Pair("type", "gamedataNewPerkType", "Cool_Left_Perk_2_1", "currLevel", 1)]));
            Field(body, "attributesData", "array:SAttributeData", Array([relic, cool]));
        }
        Field(body, "lifePath", "gamedataLifePath", Enum("Nomad"));

        blob.Objects.Add(new BlobObject(blob.Intern("PlayerDevelopmentData"), body));
        return blob;
    }

    // ---- appearance: CharacetrCustomization_Appearances ----

    /// <summary>Two creator groups with preset choices (eyes h041, jaw h064) and parts, one weapon group, one body group, and a mapping.</summary>
    static byte[] Appearance(uint v, uint archive)
    {
        var w = new RelicWriter.Common.Codecs.NodeWriter();
        void Part(ulong hash, string resource, string tag)
        {
            if (archive >= HashedPartArchive) w.U64(hash); else w.String("part" + hash);
            w.String(resource);
            if (v >= 168) w.String(tag);
            w.U32(1); w.U32(2);
        }
        void Choices() { w.U32(2); w.String("eyes"); w.String("h041"); w.U32(0); w.U32(0); w.String("jaw"); w.String("h064"); w.U32(0); w.U32(0); }
        w.U8(1); w.U32(0);
        w.U32(13); w.U8(0); w.U8(0);
        w.U32(2);
        w.String("TPP"); w.U32(1); Part(1, "h0_000_pwa__basehead__06_bl_dark", "skin_type_05"); Choices();
        w.String("character_customization"); w.U32(2); Part(2, "h0_000_pwa__basehead__06_bl_dark", "skin_type_05"); Part(3, "19_cold_white", "hair_color50"); Choices();
        w.U32(1); w.String("holstered_default_tpp"); w.U32(0); w.U32(0);
        w.U32(1); w.String("FPP_Body"); w.U32(0); w.U32(0);
        w.U32(1); w.String("holstered_default"); w.String("holstered_default_fpp"); w.String("holstered_default_tpp");
        if (v > 171) { w.Varint(1); w.String("cold_white"); }
        return w.ToArray();
    }

    // ---- inventory: item nodes, with the sub-inventory records in the gaps between them ----

    static SaveNode Inventory(uint v)
    {
        var idSize = 12 + (v >= 97 ? 1 : 0) + (v >= 190 ? 2 : 0) + (v >= 221 ? 1 : 0);
        byte[] Item(string record, uint quantity)
        {
            var id = VehicleId.FromName(record);
            var d = new byte[idSize + 5 + 4];
            BitConverter.GetBytes(id.Hash).CopyTo(d, 0);
            d[4] = id.Length;
            BitConverter.GetBytes(7u).CopyTo(d, 8); // seed
            if (v >= 97) d[12] = 3; // kind: stackable
            BitConverter.GetBytes(quantity).CopyTo(d, idSize + 5);
            return d;
        }
        byte[] Sub(ulong id, uint count) => [.. BitConverter.GetBytes(id), .. BitConverter.GetBytes(count)];

        var node = new SaveNode { Name = "inventory", Id = 4, Data = [.. BitConverter.GetBytes(2u), .. Sub(1, 2), .. new byte[idSize]] };
        node.Children.Add(new SaveNode { Name = "itemData", Id = 5, Data = Item("Items.money", 5000), After = new byte[idSize] });
        node.Children.Add(new SaveNode { Name = "itemData", Id = 5, Data = Item("Items.Preset_Yukimura_Default", 1), After = [.. Sub(1_000_000, 1), .. new byte[idSize]] });
        node.Children.Add(new SaveNode { Name = "itemData", Id = 5, Data = Item("Items.FirstAidWhiffV0", 3) });
        return node;
    }

    // ---- garage: vehicleGarageComponentPS inside PersistencySystem2 ----

    public static ulong Fnv(string s)
    {
        var h = 0xcbf29ce484222325UL;
        foreach (var c in s) h = (h ^ (byte)c) * 0x100000001b3UL;
        return h;
    }

    static byte[] Persistency()
    {
        var o = new MemoryStream();
        void U32(uint x) => o.Write(BitConverter.GetBytes(x));
        void U64(ulong x) => o.Write(BitConverter.GetBytes(x));
        U32(2); U64(0x1111);          // persistent id set
        U32(4); U32(0);               // skip size, then the skipped bytes
        U32(3);                       // entries
        U64(0);                       // an empty entry is just its zero id
        U64(0xAAAA); U64(Fnv("someOtherPS")); U32(4); U32(0xDEAD); // another system's entry

        var entry = new MemoryStream();
        void E64(ulong x) => entry.Write(BitConverter.GetBytes(x));
        void E32(uint x) => entry.Write(BitConverter.GetBytes(x));
        E64(Fnv("unlockedVehicleArray")); E64(Fnv("array:vehicleUnlockedVehicle"));
        E32((uint)OwnedVehicles.Length);
        foreach (var name in OwnedVehicles)
        {
            var id = VehicleId.FromName(name);
            E64(Fnv("vehicleID")); E64(Fnv("vehicleGarageVehicleID"));
            E64(Fnv("recordID")); E64(Fnv("TweakDBID")); E32(id.Hash); entry.WriteByte(id.Length); entry.Write(new byte[3]);
            E64(0); // end of vehicleID
            E64(0); // end of element
        }
        E64(0); // end of the properties

        U64(0xBBBB); U64(Fnv("vehicleGarageComponentPS")); U32((uint)entry.Length);
        entry.WriteTo(o);
        return o.ToArray();
    }
}
