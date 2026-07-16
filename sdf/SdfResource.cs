using System;
using Godot;

namespace GMTK2026.sdf;

public partial class SdfResource : Resource {
    public virtual SdfType SdfType => throw new NotImplementedException();
    
    public virtual byte[] GetBytes() => throw new NotImplementedException();
}