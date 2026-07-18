using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfRhombus : SdfResource {
    [Export] public float DiagonalA { get; set; }
    [Export] public float DiagonalB { get; set; }
    [Export] public float HalfHeight { get; set; }
    [Export] public float CornerRadius { get; set; }
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.Rhombus;

    public override byte[] GetBytes() {
        var data = new[] {
            DiagonalA, DiagonalB, HalfHeight, CornerRadius
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}