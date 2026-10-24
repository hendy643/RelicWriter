using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using RelicWriter.Common.Container;

// Usage: RelicWriter.Extractor --game <game dir> --saves <saves dir> [--previous <save-spec.xml>] --out <output dir>
//
// Writes <output dir>/<game build>/save-spec.xml: the previous spec (if given) updated with
//  - the game build (product version of Cyberpunk2077.exe),
//  - which node names occur in which save versions in the given saves,
//  - whether each node name is present as a string in the exe.
// Node layouts are NOT extracted: they come from reading the game's loaders (Ghidra) and stay as curated in the previous spec.
// Nodes seen for the first time are added with status="open" so the next layout pass knows what is missing.
// Also: RelicWriter.Extractor gamedata ... writes the game database scripts (see GameDataExport).
var gameData = args is ["gamedata", ..];
var opt = new Dictionary<string, string>();
for (var i = gameData ? 1 : 0; i + 1 < args.Length; i += 2) opt[args[i]] = args[i + 1];
if (gameData) return GameDataExport.Run(opt);
if (!opt.TryGetValue("--game", out var gameDir) || !opt.TryGetValue("--saves", out var savesDir) || !opt.TryGetValue("--out", out var outDir))
{
    Console.Error.WriteLine("Usage: RelicWriter.Extractor --game <game dir> --saves <saves dir> [--previous <save-spec.xml>] --out <output dir>");
    return 2;
}

var exePath = Path.Combine(gameDir, "bin", "x64", "Cyberpunk2077.exe");
var exe = File.ReadAllBytes(exePath);
var info = FileVersionInfo.GetVersionInfo(exePath);
var build = info.ProductVersion ?? throw new InvalidDataException("The exe has no product version.");

// Census of the saves: node name -> save versions it occurs in, and whether it repeats within one save.
var versions = new Dictionary<uint, uint>(); // save version -> game version
var seen = new Dictionary<string, SortedSet<uint>>();
var repeats = new HashSet<string>();
foreach (var file in Directory.GetDirectories(savesDir).Select(d => Path.Combine(d, "sav.dat")).Where(File.Exists))
{
    var save = SaveFile.Read(File.ReadAllBytes(file));
    var v = save.Header.SaveVersion;
    versions[v] = save.Header.GameVersion;
    foreach (var g in save.Walk().GroupBy(n => n.Name))
    {
        if (!seen.TryGetValue(g.Key, out var set)) seen[g.Key] = set = [];
        set.Add(v);
        if (g.Count() > 1) repeats.Add(g.Key);
    }
}

var previous = opt.GetValueOrDefault("--previous");
var doc = previous is null ? new XDocument(new XElement("saveSpec", new XAttribute("specFormat", "1"), new XElement("versions"), new XElement("nodes"))) : XDocument.Load(previous);
var root = doc.Root!;
var report = new List<string>();

root.SetAttributeValue("gameBuild", build);
root.SetAttributeValue("exe", $"Cyberpunk2077.exe product version {build}, file version {info.FileVersion}, {exe.Length:N0} bytes");

// Observed save versions (the <observed> list in <versions>).
var observed = root.Element("versions")!.Element("observed") ?? new XElement("observed");
if (observed.Parent is null) root.Element("versions")!.AddFirst(observed);
foreach (var (sv, gv) in versions.OrderBy(p => p.Key))
{
    if (observed.Elements("save").Any(s => (uint)s.Attribute("saveVersion")! == sv)) continue;
    observed.Add(new XElement("save", new XAttribute("saveVersion", sv), new XAttribute("gameVersion", gv), new XAttribute("status", "container verified")));
    report.Add($"new save version {sv} (game version {gv})");
}
var newest = observed.Elements("save").Max(s => (uint)s.Attribute("saveVersion")!);

var nodesEl = root.Element("nodes")!;
foreach (var (name, set) in seen.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
{
    var el = nodesEl.Elements("node").FirstOrDefault(n => (string?)n.Attribute("name") == name);
    if (el is null)
    {
        el = new XElement("node", new XAttribute("name", name), new XAttribute("status", "open"),
            new XElement("note", $"Added by RelicWriter.Extractor for game build {build}: seen in save versions {string.Join(", ", set)}; layout not extracted yet."));
        nodesEl.Add(el);
        report.Add($"new node {name}");
    }
    var first = Math.Min(set.Min, (uint?)el.Attribute("firstSeen") ?? uint.MaxValue);
    var last = Math.Max(set.Max, (uint?)el.Attribute("lastSeen") ?? ((uint?)el.Attribute("firstSeen") is null ? 0 : newest));
    if (first != (uint?)el.Attribute("firstSeen")) { if (el.Attribute("firstSeen") is not null) report.Add($"{name}: firstSeen {el.Attribute("firstSeen")!.Value} -> {first}"); el.SetAttributeValue("firstSeen", first); }
    var wasLast = (uint?)el.Attribute("lastSeen");
    if (last >= newest) el.Attribute("lastSeen")?.Remove(); else el.SetAttributeValue("lastSeen", last);
    if ((uint?)el.Attribute("lastSeen") != wasLast && el.Attribute("firstSeen") is not null && wasLast is not null) report.Add($"{name}: lastSeen {wasLast} -> {(uint?)el.Attribute("lastSeen")}");
    if (repeats.Contains(name)) el.SetAttributeValue("repeats", "true");
}

// Presence of each node name in the exe (the game keeps them as plain strings).
foreach (var el in nodesEl.Elements("node"))
{
    var name = (string)el.Attribute("name")!;
    var found = exe.AsSpan().IndexOf(Encoding.ASCII.GetBytes(name)) >= 0 || exe.AsSpan().IndexOf(Encoding.ASCII.GetBytes(name.Trim('"'))) >= 0;
    el.SetAttributeValue("inExe", found ? "true" : "false");
    if (!found) report.Add($"{name}: not found as a string in the exe");
}
nodesEl.SetAttributeValue("count", nodesEl.Elements("node").Count());

var target = Path.Combine(outDir, build);
Directory.CreateDirectory(target);
var targetFile = Path.Combine(target, "save-spec.xml");
doc.Save(targetFile);
Console.WriteLine($"Game build {build}: {versions.Count} save versions, {seen.Count} node names in saves, {nodesEl.Elements("node").Count()} in spec. Wrote {targetFile}");
foreach (var line in report) Console.WriteLine("  " + line);
return 0;
