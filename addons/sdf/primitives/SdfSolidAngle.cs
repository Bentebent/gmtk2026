using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfSolidAngle : SdfResource {
    [Export] public Vector2 SinCosAngle { get; set; } = Vector2.Zero;
    [Export] public float Radius { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.SolidAngle;

    public override byte[] GetBytes() {
        var data = new[] {
            SinCosAngle.X, SinCosAngle.Y, Radius
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}