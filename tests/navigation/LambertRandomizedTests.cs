using MegaCrew.Navigation;
using System;
class P {
 static Vector3d Add(Vector3d a, Vector3d b)=>a+b;
 static Vector3d Acc(Vector3d r,double mu)=>r*(-mu/Math.Pow(r.Magnitude,3));
 static (Vector3d,Vector3d) Step(Vector3d r,Vector3d v,double h,double mu){
   var k1r=v; var k1v=Acc(r,mu);
   var k2r=v+k1v*(h/2); var k2v=Acc(r+k1r*(h/2),mu);
   var k3r=v+k2v*(h/2); var k3v=Acc(r+k2r*(h/2),mu);
   var k4r=v+k3v*h; var k4v=Acc(r+k3r*h,mu);
   return (r+(k1r+k2r*2+k3r*2+k4r)*(h/6),v+(k1v+k2v*2+k3v*2+k4v)*(h/6));
 }
 static (Vector3d,Vector3d) Prop(Vector3d r,Vector3d v,double t,double mu){int n=20000; double h=t/n; for(int i=0;i<n;i++)(r,v)=Step(r,v,h,mu); return(r,v);}
 static void Main(){var rng=new Random(20261030); int good=0,failed=0; double maxPos=0,maxVel=0,maxTime=0; string worst=""; for(int k=0;k<2000;k++){
   double mu=1.0, r1m=0.8+rng.NextDouble()*0.4, r2m=0.8+rng.NextDouble()*0.4;
   double a1=rng.NextDouble()*Math.PI*2, a2=a1+(0.03+rng.NextDouble()*(2*Math.PI-0.06));
   var from=new Vector3d(r1m*Math.Cos(a1),r1m*Math.Sin(a1),0); var to=new Vector3d(r2m*Math.Cos(a2),r2m*Math.Sin(a2),0);
   double scale=Math.Sqrt(Math.Pow((r1m+r2m)/2,3)/mu); double t=scale*Math.Pow(10,rng.NextDouble()*1.7-0.7);
   try {var sol=Lambert.SolveShortWay(from,to,t,mu); var (rf,vf)=Prop(from,sol.DepartureVelocity,t,mu); double pe=(rf-to).Magnitude/Math.Max(1,to.Magnitude), ve=(vf-sol.ArrivalVelocity).Magnitude/Math.Max(1,sol.ArrivalVelocity.Magnitude);
     good++;if(pe>maxPos){maxPos=pe;worst=$"case={k} from=({from.X:R},{from.Y:R},{from.Z:R}) to=({to.X:R},{to.Y:R},{to.Z:R}) t={t:R} mu={mu:R} posRel={pe:R} velRel={ve:R} dep=({sol.DepartureVelocity.X:R},{sol.DepartureVelocity.Y:R},{sol.DepartureVelocity.Z:R})";} maxVel=Math.Max(maxVel,ve);maxTime=Math.Max(maxTime,Math.Abs(sol.TimeResidual));
     if(pe>1e-5 || ve>1e-5) {failed++; if(failed<=5)Console.WriteLine("FAIL "+worst);}
   } catch(Exception e){failed++; if(failed<=5)Console.WriteLine($"EXCEPTION case={k} angle={a2-a1:R} r1={r1m:R} r2={r2m:R} t={t:R} mu={mu:R}: {e.GetType().Name} {e.Message}");}
 } if(failed>0)throw new Exception($"{failed} randomized Lambert cases failed"); Console.WriteLine($"RESULT good={good} failed={failed} maxPosRel={maxPos:R} maxVelRel={maxVel:R} maxTimeResidual={maxTime:R}\nWORST {worst}"); }
}
