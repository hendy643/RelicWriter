using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RelicWriter.Common.GameData.Storage;

// Table and column names are the ones the data migrations in the Database folder use.
[Table("meta"), PrimaryKey(nameof(Key))]
internal sealed class MetaRow
{
    [Column("key")] public required string Key { get; set; }
    [Column("value")] public required string Value { get; set; }
}

[Table("enum_default"), PrimaryKey(nameof(Type))]
internal sealed class EnumDefaultRow
{
    [Column("type")] public required string Type { get; set; }
    [Column("value")] public required string Value { get; set; }
}

[Table("record"), PrimaryKey(nameof(Hash), nameof(Length))]
internal sealed class RecordRow
{
    [Column("hash")] public long Hash { get; set; }
    [Column("length")] public int Length { get; set; }
    [Column("name")] public required string Name { get; set; }
    [Column("display_name")] public string? DisplayName { get; set; }
}

// An item the player can hold: the in-game name, what it is for the picker's filters, and which optional parts its save node has (an ItemShape).
[Table("item"), PrimaryKey(nameof(Hash), nameof(Length))]
internal sealed class ItemRow
{
    [Column("hash")] public long Hash { get; set; }
    [Column("length")] public int Length { get; set; }
    [Column("name")] public required string Name { get; set; }
    [Column("category")] public required string Category { get; set; }
    [Column("type")] public string? Type { get; set; }
    [Column("quality")] public string? Quality { get; set; }
    [Column("description")] public string? Description { get; set; }
    [Column("shape")] public int Shape { get; set; }
    // 1: the record is only in the Phantom Liberty game files.
    [Column("expansion")] public int Expansion { get; set; }
}

[Table("vehicle"), PrimaryKey(nameof(Record))]
internal sealed class VehicleRow
{
    [Column("record")] public required string Record { get; set; }
    [Column("sort")] public int Sort { get; set; }
    // The hash of the atlas file's path in the game archives (a ulong, stored as its 64 bits) and the part of the atlas.
    [Column("atlas")] public long? Atlas { get; set; }
    [Column("atlas_part")] public string? AtlasPart { get; set; }
}

[Table("perk_tree"), PrimaryKey(nameof(Name))]
internal sealed class PerkTreeRow
{
    [Column("name")] public required string Name { get; set; }
    [Column("sort")] public int Sort { get; set; }
    [Column("attribute_data")] public required string AttributeData { get; set; }
}

[Table("perk"), PrimaryKey(nameof(Id))]
internal sealed class PerkRow
{
    [Column("id")] public required string Id { get; set; }
    [Column("tree")] public required string Tree { get; set; }
    [Column("sort")] public int Sort { get; set; }
    [Column("max_level")] public int MaxLevel { get; set; }
    [Column("cost_per_level")] public int CostPerLevel { get; set; }
    [Column("display_name")] public string? DisplayName { get; set; }
    [Column("requires")] public string Requires { get; set; } = "";
    [Column("attribute_points")] public int AttributePoints { get; set; }
}

[Table("constant"), PrimaryKey(nameof(Name))]
internal sealed class ConstantRow
{
    [Column("name")] public required string Name { get; set; }
    [Column("value")] public int Value { get; set; }
}

// The user's own values (the game folder, settings). Schema only: no migration puts rows here, so updates never touch what the user set.
[Table("local_data"), PrimaryKey(nameof(Key))]
internal sealed class LocalDataRow
{
    [Column("key")] public required string Key { get; set; }
    [Column("value")] public required string Value { get; set; }
}

/// <summary>The game database's tables. The schema is managed by the EF migrations in the Migrations folder; the app only reads it.</summary>
internal sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<MetaRow> Meta => Set<MetaRow>();
    public DbSet<EnumDefaultRow> EnumDefaults => Set<EnumDefaultRow>();
    public DbSet<RecordRow> Records => Set<RecordRow>();
    public DbSet<ItemRow> Items => Set<ItemRow>();
    public DbSet<VehicleRow> Vehicles => Set<VehicleRow>();
    public DbSet<PerkTreeRow> PerkTrees => Set<PerkTreeRow>();
    public DbSet<PerkRow> Perks => Set<PerkRow>();
    public DbSet<ConstantRow> Constants => Set<ConstantRow>();
    public DbSet<LocalDataRow> LocalData => Set<LocalDataRow>();
}

/// <summary>Lets <c>dotnet ef migrations add</c> create the context.</summary>
internal sealed class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
