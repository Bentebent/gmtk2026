using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfLink : SdfResource {
    [Export] public float ArmLength { get; set; } = 0.5f;
    [Export] public float MajorRadius { get; set; } = 0.5f;
    [Export] public float MinorRadius { get; set; } = 0.5f;

    public override SdfPrimitive SdfPrimitive => SdfPrimitive.Link;

    public override byte[] GetBytes() {
        var data = new[] {
            ArmLength, MajorRadius, MinorRadius
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}