using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfRoundedCylinder : SdfResource {
    [Export] public float Radius { get; set; } = 0.5f;
    [Export] public float RoundingRadius { get; set; }
    [Export] public float HalfHeight { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.RoundedCylinder;

    public override byte[] GetBytes() {
        var data = new[] {
            Radius, RoundingRadius, HalfHeight
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}