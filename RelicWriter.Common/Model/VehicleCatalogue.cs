using RelicWriter.Common.GameData;

namespace RelicWriter.Common.Model;

/// <summary>The vehicles the game can unlock, from the game database.</summary>
public static class VehicleCatalogue
{
    /// <summary>Where the game keeps a vehicle's picture (the picture is read from the player's game files), or null for a record not in the catalogue.</summary>
    public static VehicleEntry? Entry(string record) => GameDatabase.Current.Vehicles.FirstOrDefault(v => v.Record == record);

    public static IReadOnlyList<string> Records => GameDatabase.Current.Vehicles.Select(v => v.Record).ToList();
}
