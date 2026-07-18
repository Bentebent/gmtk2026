using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfInfiniteCylinder : SdfResource {
    [Export] public Vector2 AxisPoint { get; set; } = Vector2.Zero;
    [Export] public float Radius { get; set; } = 0.5f;

    public override SdfPrimitive SdfPrimitive => SdfPrimitive.InfiniteCylinder;

    public override byte[] GetBytes() {
        var data = new[] {
            AxisPoint.X, AxisPoint.Y, Radius
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}