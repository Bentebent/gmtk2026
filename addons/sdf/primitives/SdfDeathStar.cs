using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfDeathStar : SdfResource {
    [Export] public float RadiusA { get; set; } = 1.0f;
    [Export] public float RadiusB { get; set; } = 1.0f;
    [Export] public float SeparationDist { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.DeathStar;

    public override byte[] GetBytes() {
        var data = new[] {
            RadiusA, RadiusB, SeparationDist
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}