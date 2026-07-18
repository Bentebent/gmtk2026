using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfCone : SdfResource {
    [Export] public Vector2 SinCosAngle { get; set; } = new(0.6f, 0.8f);
    [Export] public float Height { get; set; } = 0.5f;

    public override SdfPrimitive SdfPrimitive => SdfPrimitive.Cone;

    public override byte[] GetBytes() {
        var data = new[] {
            SinCosAngle.X, SinCosAngle.Y, Height
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}