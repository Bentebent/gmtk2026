using System;
using Godot;

namespace GMTK2026.sdf;

[Tool]
[GlobalClass]
public partial class SdfSphere : SdfResource {
	[Export] public float Radius { get; set; }

	public override SdfType SdfType => SdfType.Sphere;

	public override byte[] GetBytes() {
		return BitConverter.GetBytes(Radius);
	}
}
