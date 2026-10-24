using System.Xml.Linq;
using RelicWriter.Generator;

// Usage: RelicWriter.Generator <save-spec.xml> <output directory>
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: RelicWriter.Generator <save-spec.xml> <output directory>");
    return 2;
}

var (specPath, outDir) = (args[0], args[1]);
var build = (string?)XDocument.Load(specPath).Root!.Attribute("gameBuild") ?? "unknown";
var skipped = new Dictionary<string, string>();
var nodes = Spec.Load(specPath, skipped);

var emitter = new Emitter(build);
var typeNames = nodes.Select(n => Emitter.TypeName(n.Name)).ToList();
if (typeNames.Distinct().Count() != typeNames.Count)
    throw new InvalidOperationException("Two nodes map to the same C# type name: " + string.Join(", ", typeNames.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key)));

Directory.CreateDirectory(outDir);
foreach (var old in Directory.GetFiles(outDir, "*.g.cs")) File.Delete(old);
foreach (var n in nodes)
    File.WriteAllText(Path.Combine(outDir, Emitter.TypeName(n.Name) + ".g.cs"), emitter.Emit(n));
File.WriteAllText(Path.Combine(outDir, "GeneratedCodecs.g.cs"), emitter.EmitRegistry(nodes));

Console.WriteLine($"Spec for game build {build}: generated {nodes.Count} codecs into {outDir}; skipped {skipped.Count}:");
foreach (var g in skipped.GroupBy(s => s.Value).OrderByDescending(g => g.Count()))
    Console.WriteLine($"  {g.Count(),3}  {g.Key}  ({string.Join(", ", g.Select(s => s.Key).Take(4))}{(g.Count() > 4 ? ", ..." : "")})");
return 0;
