using System;
using Godot;

namespace GMTK2026.addons.sdf.primitives;

public partial class SdfResource : Resource {
    public virtual SdfPrimitive SdfPrimitive => throw new NotImplementedException();
    
    public virtual byte[] GetBytes() => throw new NotImplementedException();
}