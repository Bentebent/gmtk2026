using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfBoxFrame : SdfResource {
    [Export] public Vector3 HalfExtents { get; set; } = new(0.5f, 0.5f, 0.5f);
    [Export] public float EdgeThickness { get; set; } = 0.1f;

    public override SdfPrimitive SdfPrimitive => SdfPrimitive.BoxFrame;

    public override byte[] GetBytes() {
        var data = new[] {
            HalfExtents.X, HalfExtents.Y, HalfExtents.Z, EdgeThickness
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}