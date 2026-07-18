using System;
using System.Collections.Concurrent;
using Godot;
using System.Collections.Generic;
namespace GMTK2026.sdf;

[Tool]
[GlobalClass]
public partial class SdfInstance3D : Node3D {
	private SdfResource _sdfResource;
	[Export] public SdfResource Resource {
		get => _sdfResource;
		private set {
			if (Resource != _sdfResource) {
				SdfRegistry.Instance.RemoveSdfInstance(this);
			}
			_sdfResource = value;
			SdfRegistry.Instance.AddSdfInstance(this);
		}
	}

	public override void _Ready() { }

	public override void _EnterTree() {
		SdfRegistry.Instance.AddSdfInstance(this);
		base._EnterTree();
	}

	public override void _ExitTree() {
		SdfRegistry.Instance.RemoveSdfInstance(this);
		base._ExitTree();
	}

	public byte[] GetInstanceData() {
		Quaternion rotation = new Quaternion(GlobalBasis).Normalized();
		var data = new []{
			GlobalPosition.X,
			GlobalPosition.Y,
			GlobalPosition.Z,
			GlobalTransform.Basis.Scale.X,
			rotation.X,
			rotation.Y,
			rotation.Z,
			rotation.W,
		};

		var bytes = new byte[data.Length * sizeof(float)];
		Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
		return bytes;
	}

	public override void _Process(double delta) { }
}
