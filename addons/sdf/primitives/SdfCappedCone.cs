using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfCappedCone : SdfResource {
    [Export] public float HalfHeight { get; set; } = 1.0f;
    [Export] public float BottomRadius { get; set; } = 1.0f;
    [Export] public float TopRadius { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.CappedCone;

    public override byte[] GetBytes() {
        var data = new[] {
            HalfHeight, BottomRadius, TopRadius
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}