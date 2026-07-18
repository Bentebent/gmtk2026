using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfCutSphere : SdfResource {
    [Export] public float Radius { get; set; } = 1.0f;
    [Export] public float CutHeight { get; set; } = 1.0f;
    public override SdfPrimitive SdfPrimitive => SdfPrimitive.CutSphere;

    public override byte[] GetBytes() {
        var data = new[] {
            Radius, CutHeight
        };

        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}