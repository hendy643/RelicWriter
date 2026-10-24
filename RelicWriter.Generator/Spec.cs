using System.Xml.Linq;

namespace RelicWriter.Generator;

/// <summary>A field of a node layout as written in save-spec.xml (only the kinds the generator can emit).</summary>
sealed record Field(string Kind, string Name, string? When, string? Count, string? CountField, string? ElementType, int ElementBytes, int Size, List<Field>? Element);

sealed record NodeSpec(string Name, string Status, List<Field> Fields);

static class Spec
{
    static readonly HashSet<string> Primitives = ["u8", "u16", "u32", "u64", "i32", "f32"];
    static readonly HashSet<string> ElementPrimitives = [.. Primitives, "string", "name"];

    /// <summary>Reads every node entry; nodes the generator cannot emit are returned in <paramref name="skipped"/> with the reason.</summary>
    public static List<NodeSpec> Load(string path, Dictionary<string, string> skipped)
    {
        var nodes = new List<NodeSpec>();
        foreach (var n in XDocument.Load(path).Root!.Element("nodes")!.Elements("node"))
        {
            var name = (string)n.Attribute("name")!;
            var problems = new List<string>();
            if (n.Attribute("format") is { } fmt) problems.Add($"format {fmt.Value}");
            if (n.Element("trailing") is not null) problems.Add("data after the children");
            if (n.Element("fields") is not { } fields) problems.Add("no layout");
            else if (!fields.Elements().Any(e => e.Name != "note")) problems.Add("holds only the node id");
            var list = problems.Count == 0 ? Parse(n.Element("fields")!, problems) : [];
            if (problems.Count > 0) skipped[name] = string.Join("; ", problems.Distinct());
            else nodes.Add(new NodeSpec(name, (string?)n.Attribute("status") ?? "", list));
        }
        return nodes;
    }

    static List<Field> Parse(XElement parent, List<string> problems)
    {
        var list = new List<Field>();
        foreach (var e in parent.Elements())
        {
            var kind = e.Name.LocalName;
            if (kind is "note" or "open" or "evidence") continue;
            var name = (string?)e.Attribute("name") ?? "";
            var when = (string?)e.Attribute("when");
            if (Primitives.Contains(kind) || kind is "string" or "name" or "cname")
                list.Add(new Field(kind, name, when, null, null, null, 0, 0, null));
            else if (kind == "bytes")
                list.Add(new Field(kind, name, when, null, null, null, 0, (int)e.Attribute("size")!, null));
            else if (kind == "array")
            {
                var elementType = (string?)e.Attribute("elementType");
                var elementBytes = (int?)e.Attribute("elementBytes") ?? 0;
                List<Field>? element = null;
                if (e.Element("element") is { } el) element = Parse(el, problems);
                else if (elementType is null && elementBytes == 0) problems.Add($"array {name} without element");
                else if (elementType is not null && !ElementPrimitives.Contains(elementType)) problems.Add($"array {name} of type {elementType}");
                list.Add(new Field(kind, name, when, (string?)e.Attribute("count"), (string?)e.Attribute("countField"), elementType, elementBytes, 0, element));
            }
            else problems.Add($"element {kind}");
        }
        return list;
    }
}
