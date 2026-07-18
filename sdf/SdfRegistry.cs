using System;
using System.Collections.Generic;
using Godot;

namespace GMTK2026.sdf;

[Tool]
public partial class SdfRegistry : Node {
    public static SdfRegistry Instance { get; private set; }
    public Dictionary<SdfType, HashSet<SdfInstance3D>> SdfInstances { get; private set; }

    public override void _Ready() {
        Instance = this;
        SdfInstances = [];
        foreach (SdfType sdfType in Enum.GetValues(typeof(SdfType))) {
            SdfInstances.Add(sdfType, []);
        }
    }

    public override void _EnterTree() { }

    public void ResetRegistry() {
        SdfInstances.Clear();
    }

    public void AddSdfInstance(SdfType sdfType, SdfInstance3D instance) {
        SdfInstances[sdfType].Add(instance);
    }

    public void RemoveSdfInstance(SdfType sdfType, SdfInstance3D instance) {
        SdfInstances[sdfType].Remove(instance);
    }
}