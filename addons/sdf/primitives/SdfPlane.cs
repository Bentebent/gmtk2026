using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfPlane : SdfResource {
    [Export] public Vector3 Normal { get; set; } = new(0.0f, 1.0f, 0.0f);
    [Export] public float Offset { get; set; }
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.Plane;

    public override byte[] GetBytes() {
        var n = Normal.Normalized();
        var data = new[] {
            n.X, n.Y, n.Z, Offset
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}