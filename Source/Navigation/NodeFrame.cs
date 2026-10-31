using System;
namespace MegaCrew.Navigation
{
    public struct NodeComponents { public double RadialPlus, NormalMinus, Prograde; }
    public static class NodeFrame
    {
        static Vector3d Unit(Vector3d v)
        {
            double n=v.Magnitude;
            if(double.IsNaN(n)||double.IsInfinity(n)||n<1e-14)throw new ArgumentException("Degenerate or nonfinite vector");
            return v/n;
        }
        static Vector3d Cross(Vector3d a,Vector3d b){return new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        // All inputs must share the same right-handed body-centered inertial frame.
        public static NodeComponents Decompose(Vector3d position,Vector3d velocity,Vector3d deltaV)
        {
            if(double.IsNaN(deltaV.Magnitude)||double.IsInfinity(deltaV.Magnitude))throw new ArgumentException("Nonfinite delta-v");
            var r=Unit(position);var v=Unit(velocity);var normal=Unit(Cross(r,v));
            var radial=Unit(r-v*Vector3d.Dot(r,v));
            return new NodeComponents{RadialPlus=Vector3d.Dot(radial,deltaV),NormalMinus=-Vector3d.Dot(normal,deltaV),Prograde=Vector3d.Dot(v,deltaV)};
        }
        public static Vector3d Reconstruct(Vector3d position,Vector3d velocity,NodeComponents c)
        {
            var r=Unit(position);var v=Unit(velocity);var normal=Unit(Cross(r,v));var radial=Unit(r-v*Vector3d.Dot(r,v));
            return radial*c.RadialPlus-normal*c.NormalMinus+v*c.Prograde;
        }
    }
}
