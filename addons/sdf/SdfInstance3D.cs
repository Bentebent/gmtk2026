using System;
using Godot;
using GMTK2026.addons.sdf.primitives;

namespace GMTK2026.addons.sdf;

[Tool]
[GlobalClass]
public partial class SdfInstance3D : Node3D {
	private SdfResource _sdfResource;
	private SdfPrimitive? _oldType;
	private bool _updatedResource;
	
	[Export] public SdfResource Resource {
		get => _sdfResource;
		private set {
			if (_sdfResource != value) {
				if (_sdfResource != null) {
					_oldType = _sdfResource.SdfPrimitive;
				}
				_sdfResource = value;
				_updatedResource = true;
			}
		}
	}

	public override void _Ready() {
		if (_sdfResource == null) {
			return;
		}
		SdfRegistry.Instance.AddSdfInstance(_sdfResource.SdfPrimitive, this);
		
		base._Ready();
	}

	public override void _ExitTree() {
		if (_sdfResource == null) {
			return;
		}
		SdfRegistry.Instance.RemoveSdfInstance(_sdfResource.SdfPrimitive, this);
		
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

	public override void _Process(double delta) {
		if (_updatedResource) {
			if (_oldType != null) {
				SdfRegistry.Instance.RemoveSdfInstance(_oldType.Value, this);	
				_oldType = null;
			}
			SdfRegistry.Instance.AddSdfInstance(_sdfResource.SdfPrimitive, this);
			_updatedResource = false;
		}
	}
}
