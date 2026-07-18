using System;
using Godot;

namespace GMTK2026.sdf;

[Tool]
[GlobalClass]
public partial class SdfBox : SdfResource {
   [Export] public Vector3 HalfSize { get; set; } = new (0.5f, 0.5f, 0.5f);
   public override SdfType SdfType => SdfType.Box;

   public override byte[] GetBytes() {
       var data = new [] {
           HalfSize.X, HalfSize.Y, HalfSize.Z,
       };
       
       var bytes = new byte[data.Length * sizeof(float)];
       Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
       return bytes;
   }
}