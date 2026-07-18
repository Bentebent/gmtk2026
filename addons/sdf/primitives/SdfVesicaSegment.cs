using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfVesicaSegment : SdfResource {
    [Export] public Vector3 EndA { get; set; } = new(0.5f, 0.5f, 0.5f);
    [Export] public Vector3 EndB { get; set; } = new(0.5f, 0.5f, 0.5f);
    [Export] public float Width { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.VesicaSegment;

    public override byte[] GetBytes() {
        var data = new[] {
            EndA.X, EndA.Y, EndA.Z, EndB.X, EndB.Y, EndB.Z, Width
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}