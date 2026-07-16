using System;
using System.Collections.Generic;
using Godot;

namespace GMTK2026.sdf;

[Tool]
public partial class SdfRegistry : Node {
    public static SdfRegistry Instance { get; private set; }
    public Dictionary<SdfType, List<SdfInstance3D>> SdfInstances { get; private set; }

    public override void _Ready() {
        Instance = this;
        SdfInstances = [];
        foreach (SdfType sdfType in Enum.GetValues(typeof(SdfType))) {
            SdfInstances.Add(sdfType, []);
        }
    }

    public void ResetRegistry() {
        SdfInstances.Clear();
    }

    public void AddSdfInstance(SdfInstance3D instance) {
        if (instance.Resource == null) {
            GD.PrintErr("SdfRegistry.AddSdfInstance: Resource is null");
            return;
        }

        SdfInstances[instance.Resource.SdfType].Add(instance);
    }

    public void RemoveSdfInstance(SdfInstance3D instance) {
        if (instance.Resource == null) {
            GD.PrintErr("SdfRegistry.AddSdfInstance: Resource is null");
            return;
        }
        
        SdfInstances[instance.Resource.SdfType].Remove(instance);
    }
}