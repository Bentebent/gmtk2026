using System;
using System.Collections.Generic;
using Godot;

namespace GMTK2026.addons.sdf;

[Tool]
public partial class SdfRegistry : Node {
    public static SdfRegistry Instance { get; private set; }
    public Dictionary<SdfPrimitive, HashSet<SdfInstance3D>> SdfInstances { get; private set; }

    public override void _Ready() {
        Instance = this;
        SdfInstances = [];
        foreach (SdfPrimitive sdfType in Enum.GetValues(typeof(SdfPrimitive))) {
            SdfInstances.Add(sdfType, []);
        }
    }

    public override void _EnterTree() { }

    public void ResetRegistry() {
        SdfInstances.Clear();
    }

    public void AddSdfInstance(SdfPrimitive sdfPrimitive, SdfInstance3D instance) {
        SdfInstances[sdfPrimitive].Add(instance);
    }

    public void RemoveSdfInstance(SdfPrimitive sdfPrimitive, SdfInstance3D instance) {
        SdfInstances[sdfPrimitive].Remove(instance);
    }
}