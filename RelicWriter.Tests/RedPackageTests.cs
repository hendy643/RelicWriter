using System.Text;
using RelicWriter.Model3D.Resources;
using Xunit;

namespace RelicWriter.Tests;

public class RedPackageTests
{
    const int Base = 0x4C;

    /// <summary>A package with one path and one object of class T holding a path reference and a name.</summary>
    static byte[] Build()
    {
        var names = new[] { "T", "mesh", "raRef:CMesh", "name", "CName", "hello" };
        var path = "ab"u8.ToArray();
        var body = new List<byte>();
        void U16(int v) => body.AddRange(BitConverter.GetBytes((ushort)v));
        void U32(uint v) => body.AddRange(BitConverter.GetBytes(v));

        U32(1u << 24); // one path descriptor: offset 4 is patched below, length byte = 1 (x2)
        var pathAt = body.Count; body.AddRange(path);
        var descAt = body.Count; // name descriptors come after the path pool
        var poolAt = descAt + names.Length * 4;
        var pool = new List<byte>();
        foreach (var n in names)
        {
            U32((uint)(poolAt + pool.Count) | (uint)(n.Length + 1) << 24);
            pool.AddRange(Encoding.ASCII.GetBytes(n)); pool.Add(0);
        }
        body.AddRange(pool);
        var tableAt = body.Count;
        U32(0); U32((uint)(tableAt + 8)); // object table: class 0, data right after
        U16(2);
        U16(1); U16(2); U32(18);
        U16(3); U16(4); U32(20);
        U16(0); U16(5);

        var data = new byte[Base + body.Count];
        body.CopyTo(data, Base);
        BitConverter.GetBytes((uint)pathAt).CopyTo(data, Base); // path descriptor: offset of the pool, length 2
        data[Base + 3] = 1;
        BitConverter.GetBytes((uint)pathAt).CopyTo(data, Base);
        data[Base + 3] = 1;
        BitConverter.GetBytes((uint)pathAt).CopyTo(data, Base);
        data[Base + 3] = 1;
        BitConverter.GetBytes((uint)(pathAt + path.Length)).CopyTo(data, 0x10);
        BitConverter.GetBytes((uint)pathAt).CopyTo(data, 0x0C);
        BitConverter.GetBytes((uint)poolAt).CopyTo(data, 0x14);
        BitConverter.GetBytes((uint)tableAt).CopyTo(data, 0x18);
        BitConverter.GetBytes((uint)(tableAt + 8)).CopyTo(data, 0x1C);
        return data;
    }

    [Fact]
    public void ReadsPathsNamesAndObjects()
    {
        var package = RedPackage.Parse(Build())!;
        Assert.Equal(["ab"], package.Paths);
        var obj = Assert.Single(package.Objects);
        Assert.Equal("T", obj.ClassName);
        Assert.Equal("ab", obj.Get<RedRef>("mesh")!.Path);
        Assert.Equal("hello", obj.Text("name"));
    }
}
