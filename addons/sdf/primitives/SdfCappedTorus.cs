using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfCappedTorus : SdfResource {
    [Export] public float MajorRadius { get; set; } = 0.5f;
    [Export] public float MinorRadius { get; set; } = 0.5f;
    [Export] public Vector2 SinCosAngle { get; set; } = new(0.0f, 0.0f);

    public override SdfPrimitive SdfPrimitive => SdfPrimitive.CappedTorus;

    public override byte[] GetBytes() {
        var data = new[] {
            MajorRadius, MinorRadius, SinCosAngle.X, SinCosAngle.Y
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}