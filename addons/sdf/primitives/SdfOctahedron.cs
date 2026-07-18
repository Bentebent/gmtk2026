using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfOctahedron : SdfResource {
    [Export] public float Size { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.Octahedron;

    public override byte[] GetBytes() {
        var data = new[] {
            Size
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}