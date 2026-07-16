using Godot;

namespace GMTK2026.sdf;

[Tool]
[GlobalClass]
public partial class SdfInstance3D : Node3D {
	[Export] public SdfResource Resource { get; private set; }

	public override void _Ready() { }

	public override void _EnterTree() {
		SdfRegistry.Instance.AddSdfInstance(this);
		base._EnterTree();
	}

	public override void _ExitTree() {
		SdfRegistry.Instance.RemoveSdfInstance(this);
		base._ExitTree();
	}

	public override void _Process(double delta) { }
}
