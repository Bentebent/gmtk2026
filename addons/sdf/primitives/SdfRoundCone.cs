using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfRoundCone : SdfResource {
    [Export] public float BottomRadius { get; set; } = 1.0f;
    [Export] public float TopRadius { get; set; } = 1.0f;
    [Export] public float Height { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.RoundCone;

    public override byte[] GetBytes() {
        var data = new[] {
            BottomRadius, TopRadius, Height
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}