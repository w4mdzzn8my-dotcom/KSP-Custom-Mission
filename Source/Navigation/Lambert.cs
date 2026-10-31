using System;
namespace MegaCrew.Navigation
{
    public struct Vector3d
    {
        public double X,Y,Z;
        public Vector3d(double x,double y,double z){X=x;Y=y;Z=z;}
        public double Magnitude { get { return Math.Sqrt(X*X+Y*Y+Z*Z); } }
        public static Vector3d operator +(Vector3d a,Vector3d b){return new Vector3d(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        public static Vector3d operator -(Vector3d a,Vector3d b){return new Vector3d(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        public static Vector3d operator *(Vector3d a,double b){return new Vector3d(a.X*b,a.Y*b,a.Z*b);}
        public static Vector3d operator /(Vector3d a,double b){return a*(1/b);}
        public static double Dot(Vector3d a,Vector3d b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
    }
    public struct LambertResult { public Vector3d DepartureVelocity,ArrivalVelocity; public double TimeResidual; }
    /// <summary>Zero-revolution, short-way, two-body Lambert solution. No SOI transitions.</summary>
    public static class Lambert
    {
        static bool Finite(double x){return !double.IsInfinity(x)&&!double.IsNaN(x);}
        static double C(double z){if(Math.Abs(z)<1e-6)return .5-z/24+z*z/720; if(z>0){var s=Math.Sqrt(z);return (1-Math.Cos(s))/z;}else{var s=Math.Sqrt(-z);return (Math.Cosh(s)-1)/(-z);}}
        static double S(double z){if(Math.Abs(z)<1e-6)return 1.0/6-z/120+z*z/5040; if(z>0){var s=Math.Sqrt(z);return (s-Math.Sin(s))/(s*s*s);}else{var s=Math.Sqrt(-z);return (Math.Sinh(s)-s)/(s*s*s);}}
        static double Time(double z,double r1,double r2,double A,double mu,out double y)
        {
            double c=C(z),s=S(z);y=double.NaN;
            if(!Finite(c)||!Finite(s)||c<=0)return double.NaN;
            y=r1+r2+A*(z*s-1)/Math.Sqrt(c);
            if(y<0||!Finite(y))return double.NaN;
            double x=Math.Sqrt(y/c), t=(x*x*x*s+A*Math.Sqrt(y))/Math.Sqrt(mu);
            return Finite(t)?t:double.NaN;
        }
        public static LambertResult SolveShortWay(Vector3d from,Vector3d to,double seconds,double mu)
        {
            double r1=from.Magnitude,r2=to.Magnitude;
            if(!Finite(r1)||!Finite(r2)||!Finite(seconds)||!Finite(mu)||r1<=0||r2<=0||seconds<=0||mu<=0)throw new ArgumentOutOfRangeException("Invalid transfer inputs");
            double cos=Math.Max(-1,Math.Min(1,Vector3d.Dot(from,to)/(r1*r2)));
            double sin=Math.Sqrt(Math.Max(0,1-cos*cos));
            if(sin<1e-8)throw new ArgumentException("Collinear endpoints are ill-conditioned");
            double A=sin*Math.Sqrt(r1*r2/(1-cos));
            double low=-4*Math.PI*Math.PI, high=4*Math.PI*Math.PI-1e-7, lo=0,hi=0,prevZ=0,prevF=0;bool previous=false,bracket=false;
            for(int i=0;i<=4096;i++){
                double z=low+(high-low)*i/4096, y;double f=Time(z,r1,r2,A,mu,out y)-seconds;
                if(!Finite(f))continue;
                if(previous && prevF<=0 && f>=0){lo=prevZ;hi=z;bracket=true;break;}
                previous=true;prevZ=z;prevF=f;
            }
            if(!bracket)throw new InvalidOperationException("No well-conditioned zero-revolution short-way solution in search interval");
            double mid=0,yy=0,actual=0;
            for(int i=0;i<100;i++){
                mid=(lo+hi)/2;actual=Time(mid,r1,r2,A,mu,out yy);
                if(!Finite(actual))throw new InvalidOperationException("Lambert iteration failed");
                if(actual<seconds)lo=mid;else hi=mid;
            }
            double f1=1-yy/r1,g=A*Math.Sqrt(yy/mu),gdot=1-yy/r2;
            if(!Finite(g)||Math.Abs(g)<1e-10)throw new InvalidOperationException("Degenerate Lambert geometry");
            return new LambertResult{DepartureVelocity=(to-from*f1)/g,ArrivalVelocity=(to*gdot-from)/g,TimeResidual=actual-seconds};
        }
    }
}
