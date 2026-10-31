using System;
using MegaCrew.Navigation;
class MissionControlMathTests
{
    static void Yes(bool v){if(!v)throw new Exception("assertion failed");}
    static void Near(double a,double b,double tol){if(Math.Abs(a-b)>tol)throw new Exception(a+" != "+b);}
    static void Reject(Action a){try{a();}catch(ArgumentOutOfRangeException){return;}throw new Exception("Expected rejection");}
    static Vector3d Acc(Vector3d p,double mu){return p*(-mu/(p.Magnitude*p.Magnitude*p.Magnitude));}
    static Vector3d Propagate(Vector3d p,Vector3d v,double time,double mu)
    {
        int steps=20000;double dt=time/steps;
        for(int i=0;i<steps;i++){
            var a1=Acc(p,mu);var k1=v;
            var a2=Acc(p+k1*(dt/2),mu);var k2=v+a1*(dt/2);
            var a3=Acc(p+k2*(dt/2),mu);var k3=v+a2*(dt/2);
            var a4=Acc(p+k3*dt,mu);var k4=v+a3*dt;
            p=p+(k1+k2*2+k3*2+k4)*(dt/6);v=v+(a1+a2*2+a3*2+a4)*(dt/6);
        }return p;
    }
    static void Main()
    {
        Near(MissionControlMath.RoundTripSeconds(299792458,299792458),2,1e-12);
        Near(MissionControlMath.RoundTripSeconds(0,299792458),1,1e-12);
        Reject(()=>MissionControlMath.RoundTripSeconds(-1,1));
        Reject(()=>MissionControlMath.RoundTripSeconds(double.NaN,1));
        Yes(!double.IsInfinity(MissionControlMath.RoundTripSeconds(double.MaxValue,double.MaxValue)));
        const double mu=3.5316e12, body=600000, r=750000, altitude=30000;
        double delta=MissionControlMath.CircularApoapsisBurn(mu,body,r,altitude);
        Yes(delta<0);
        // Independently reconstruct orbit elements from energy and angular momentum.
        double speed=Math.Sqrt(mu/r)+delta, energy=speed*speed/2-mu/r;
        double a=-mu/(2*energy), h=r*speed, e=Math.Sqrt(1+2*energy*h*h/(mu*mu));
        Near(a*(1-e),body+altitude,1e-6);
        Near(a*(1+e),r,1e-6);
        Reject(()=>MissionControlMath.CircularApoapsisBurn(mu,body,r,150000));
        Reject(()=>MissionControlMath.CircularApoapsisBurn(mu,body,r,-1));
        Reject(()=>MissionControlMath.CircularApoapsisBurn(0,body,r,0));
        Reject(()=>MissionControlMath.CircularApoapsisBurn(mu,body,double.PositiveInfinity,0));
        Reject(()=>MissionControlMath.CircularApoapsisBurn(mu,body,r,double.MaxValue));
        // Quarter-circle Kepler arc: analytic endpoints and velocities.
        double radius=700000, n=Math.Sqrt(mu/(radius*radius*radius)), vc=Math.Sqrt(mu/radius);
        var sol=Lambert.SolveShortWay(new Vector3d(radius,0,0),new Vector3d(0,radius,0),Math.PI/(2*n),mu);
        Near(sol.DepartureVelocity.X,0,1e-5);Near(sol.DepartureVelocity.Y,vc,1e-5);
        Near(sol.ArrivalVelocity.X,-vc,1e-5);Near(sol.ArrivalVelocity.Y,0,1e-5);Near(sol.TimeResidual,0,1e-6);
        var departure=new Vector3d(700000,0,0);var target=new Vector3d(450000,779422.8634059948,0);
        foreach(double duration in new[]{300.0, 1200.0, 3000.0}) {
            var eccentric=Lambert.SolveShortWay(departure,target,duration,mu);
            Yes((Propagate(departure,eccentric.DepartureVelocity,duration,mu)-target).Magnitude<0.1);
        }
        Reject(()=>Lambert.SolveShortWay(new Vector3d(radius,0,0),new Vector3d(radius,0,0),0,mu));
        var rvec=new Vector3d(700000,0,0);var vvec=new Vector3d(0,2300,0);var impulse=new Vector3d(1,2,3);
        var components=NodeFrame.Decompose(rvec,vvec,impulse);
        Near(components.RadialPlus,1,1e-12);Near(components.NormalMinus,-3,1e-12);Near(components.Prograde,2,1e-12);
        var er=new Vector3d(4,0,0);var ev=new Vector3d(3,4,2);var edv=new Vector3d(-8.2,.75,5.4);
        Yes((NodeFrame.Reconstruct(er,ev,NodeFrame.Decompose(er,ev,edv))-edv).Magnitude<1e-12);
        try { NodeFrame.Decompose(new Vector3d(1,0,0),new Vector3d(2,0,0),impulse); throw new Exception("parallel vectors accepted"); } catch(ArgumentException) {}
        var fp=PlanFingerprint.Compute("v","n","Kerbin","target","rnp",1000,1,2,3);
        Yes(fp!=PlanFingerprint.Compute("v2","n","Kerbin","target","rnp",1000,1,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n2","Kerbin","target","rnp",1000,1,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Mun","target","rnp",1000,1,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Kerbin","other","rnp",1000,1,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Kerbin","target","other",1000,1,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Kerbin","target","rnp",1001,1,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Kerbin","target","rnp",1000,4,2,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Kerbin","target","rnp",1000,1,4,3));
        Yes(fp!=PlanFingerprint.Compute("v","n","Kerbin","target","rnp",1000,1,2,4));
        var approvals=new BurnApprovals((id,role)=>(id=="h"&&role=="Hamish")||(id=="b"&&role=="Basil"),1);
        approvals.Connection(true);approvals.Revise("plan-A",1000);approvals.Request("req",100,2,30);
        Yes(!approvals.Approve("h","Hamish","req",1,"plan-A",101));
        Yes(approvals.Approve("h","Hamish","req",1,"plan-A",102));
        Yes(!approvals.Approve("h","Basil","req",1,"plan-A",102));
        Yes(approvals.Approve("b","Basil","req",1,"plan-A",103));Yes(approvals.Ready("plan-A",104));
        Yes(!approvals.Ready("changed",104));Yes(!approvals.Ready("plan-A",104));Yes(!approvals.Ready("plan-A",130));
        approvals.Revise("plan-B",1001);Yes(!approvals.Ready("plan-B",104));
        approvals.Request("req2",105,1,30);Yes(approvals.Approve("h","Hamish","req2",2,"plan-B",106));
        approvals.Connection(false);approvals.Connection(true);Yes(!approvals.Approve("b","Basil","req2",2,"plan-B",106));
        try { approvals.Request("req2",107,1,30); throw new Exception("reused request accepted"); } catch(InvalidOperationException) {}
        try { approvals.Request("req3",107,0,30); throw new Exception("delay floor bypassed"); } catch(ArgumentException) {}
        var windowGate=new BurnApprovals((id,role)=>(id=="h"&&role=="Hamish")||(id=="b"&&role=="Basil"),0);
        windowGate.Connection(true);windowGate.Revise("window-plan",120);
        try { windowGate.Request("ends-at-burn",100,20,30); throw new Exception("round trip ending exactly at burn accepted"); } catch(ArgumentException) {}
        try { windowGate.Request("past-burn",100,20.001,30); throw new Exception("round trip ending after burn accepted"); } catch(ArgumentException) {}
        try { windowGate.Request("expires-at-ready",100,5,5); throw new Exception("round trip ending exactly at expiry accepted"); } catch(ArgumentException) {}
        windowGate.Request("finishes-before-burn",100,19.999,30);
        Yes(windowGate.Approve("h","Hamish","finishes-before-burn",1,"window-plan",119.999));
        Yes(!windowGate.Ready("window-plan",120));
        var overflowGate=new BurnApprovals((id,role)=>true,0);
        overflowGate.Connection(true);overflowGate.Revise("overflow-plan",double.MaxValue);
        try { overflowGate.Request("overflowed-round-trip",double.MaxValue/2,double.MaxValue/2,double.MaxValue/2); throw new Exception("overflowed approval window accepted"); } catch(ArgumentException) {}
        try { windowGate.Request("negative-ut",-1,0,1); throw new Exception("negative request UT accepted"); } catch(ArgumentException) {}
        Yes(!windowGate.Approve("h","Hamish","finishes-before-burn",1,"window-plan",-1));
        Yes(!windowGate.Ready("window-plan",-1));
        // Exercise boundary gates with a fully approved request, not a previously invalidated one.
        var gate=new BurnApprovals((id,role)=>(id=="h"&&role=="Hamish")||(id=="b"&&role=="Basil"),2);
        gate.Connection(true);gate.Revise("exact",200);gate.Request("window",100,2,10);
        Yes(gate.Approve("h","Hamish","window",1,"exact",102));
        Yes(gate.Approve("b","Basil","window",1,"exact",102));
        Yes(gate.Ready("exact",109.999));Yes(!gate.Ready("exact",110));
        Yes(!gate.Approve("b","Basil","window",1,"exact",110));
        Yes(!gate.Ready("exact",double.NaN));Yes(!gate.Ready("exact",double.PositiveInfinity));
        gate.Revise("burn",105);gate.Request("near-burn",100,2,20);
        Yes(gate.Approve("h","Hamish","near-burn",2,"burn",102));
        Yes(gate.Approve("b","Basil","near-burn",2,"burn",102));
        Yes(gate.Ready("burn",104.999));Yes(!gate.Ready("burn",105));
        var revoked=false;var revocable=new BurnApprovals((id,role)=>!revoked&&((id=="h"&&role=="Hamish")||(id=="b"&&role=="Basil")));
        revocable.Connection(true);revocable.Revise("r",200);revocable.Request("r1",100,0,20);
        Yes(revocable.Approve("h","Hamish","r1",1,"r",100));Yes(revocable.Approve("b","Basil","r1",1,"r",100));
        Yes(revocable.Ready("r",101));revoked=true;Yes(!revocable.Ready("r",101));
        Console.WriteLine("PASS: light time, periapsis reconstruction, circular Lambert and independent RK4 propagation.");
    }
}
