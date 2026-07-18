using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

[Tool]
[GlobalClass]
public partial class SdfSphere : SdfResource {
    [Export] public float Radius { get; set; } = 1.0f;

    public override SdfPrimitive SdfPrimitive => SdfPrimitive.Sphere;

    public override byte[] GetBytes() {
        return BitConverter.GetBytes(Radius);
    }
}