using System;
using System.Collections.Generic;
using MegaCrew;
using MegaCrew.DunaMission;
using MegaCrew.Navigation;
class DunaRoutePlannerTests
{
    const double Day=86400;
    static int checks;
    static void Yes(bool b,string why){checks++;if(!b)throw new Exception(why);}
    static void Eq(double a,double b,double tol=1e-8){Yes(Math.Abs(a-b)<=tol*Math.Max(1,Math.Abs(b)),a+" != "+b);}
    static DunaSearchInputs Inputs(double wet=300000,double dry=100000)
    {return new DunaSearchInputs(0,1e18,3e12,3e11,750000,420000,wet,dry,120000,800,60,15,40,80,20,30,10);}
    static void Reject(Action a){try{a();}catch(ArgumentException){checks++;return;}throw new Exception("Expected input refusal");}
    static PlanetState Circular(bool duna,double ut)
    {
        double r=duna?1.5e10:1e10,omega=Math.Sqrt(1e18/(r*r*r)),angle=omega*ut+(duna?.5:0),v=r*omega;
        return new PlanetState(new Vector3d(r*Math.Cos(angle),r*Math.Sin(angle),0),new Vector3d(-v*Math.Sin(angle),v*Math.Cos(angle),0));
    }
    static Vector3d Accel(Vector3d r){double n=r.Magnitude;return r*(-1e18/(n*n*n));}
    static PlanetState Integrate(PlanetState initial,double seconds)
    {
        // Independent Cartesian RK4 propagation: does not use Lambert's
        // universal variables or its time residual as the endpoint oracle.
        Vector3d r=initial.Position,v=initial.Velocity;double h=seconds/4000;
        for(int i=0;i<4000;i++)
        {
            var r1=v;var v1=Accel(r);
            var r2=v+v1*(h/2);var v2=Accel(r+r1*(h/2));
            var r3=v+v2*(h/2);var v3=Accel(r+r2*(h/2));
            var r4=v+v3*h;var v4=Accel(r+r3*h);
            r=r+(r1+r2*2+r3*2+r4)*(h/6);v=v+(v1+v2*2+v3*2+v4)*(h/6);
        }
        return new PlanetState(r,v);
    }
    static void CheckArc(TransferLeg leg,bool outward)
    {
        var from=Circular(!outward,leg.DepartureUT);var to=Circular(outward,leg.ArrivalUT);
        var solution=Lambert.SolveShortWay(from.Position,to.Position,leg.ArrivalUT-leg.DepartureUT,1e18);
        var final=Integrate(new PlanetState(from.Position,solution.DepartureVelocity),leg.ArrivalUT-leg.DepartureUT);
        Yes((final.Position-to.Position).Magnitude/to.Position.Magnitude<1e-7,"Independent propagated endpoint must meet planet ephemeris");
        Yes((final.Velocity-solution.ArrivalVelocity).Magnitude/solution.ArrivalVelocity.Magnitude<1e-7,"Independent propagated velocity must meet arrival velocity");
    }
    static void Main()
    {
        Eq(DunaRouteMath.ParkingDeltaV(0,3e12,750000),(Math.Sqrt(2)-1)*2000);
        Eq(DunaRouteMath.ParkingDeltaV(1500,3e12,750000),Math.Sqrt(1500*1500+8000000)-2000);
        Reject(()=>DunaRouteMath.ParkingDeltaV(double.NaN,1,1));Reject(()=>DunaRouteMath.ParkingDeltaV(1,1,0));
        Reject(()=>new DunaSearchInputs(0,1,1,1,1,1,1,1,1,1));
        Reject(()=>new DunaSearchInputs(0,1,1,1,1,1,2,1,1,1,720,1,1,360,1,1));
        Reject(()=>new TransferLeg(1,1,1,1));
        var p=Inputs();var outbound=new TransferLeg(30*Day,90*Day,500,500);var inbound=new TransferLeg(510*Day,570*Day,500,500);
        var plan=DunaRouteMath.Evaluate(p,outbound,inbound);Yes(plan!=null,"Known watch-compatible plan should survive");
        double mass=p.WetKg,total=0,durations=0;
        foreach(var b in plan.Burns)
        {
            double ratio=Math.Exp(-b.DeltaV/(800*9.80665));mass*=ratio;total+=b.DeltaV;
            Eq(b.RemainingKg,mass);Eq(b.DurationSeconds,((b.RemainingKg/ratio)-b.RemainingKg)*800*9.80665/120000);durations+=b.DurationSeconds;
        }
        Eq(plan.TotalDeltaV,total);Eq(plan.RemainingKg,mass);Eq(plan.EstimatedExtraPlaySeconds,durations*(1-1/540.0));
        foreach(var range in plan.EpochWindows)foreach(double epoch in new[]{range.EarliestUT,(range.EarliestUT+range.LatestUT)/2,range.LatestUT})
            foreach(var b in plan.Burns)
            {
                Yes(CampaignTiming.CoversBurn(epoch,b.EncounterUT,b.DurationSeconds/2,p.AlignmentSeconds+p.EncounterReserveSeconds+b.DurationSeconds/2,p.VerificationSeconds+p.EncounterReserveSeconds),"Whole reserved window must be inside human watch");
                Yes(epoch<=outbound.DepartureUT,"Do not shift physical route events");
            }
        Eq(outbound.DepartureUT,30*Day);Eq(inbound.ArrivalUT,570*Day);
        foreach(var e in plan.EpochWindows)foreach(double epoch in new[]{e.EarliestUT,e.LatestUT})
        {Yes(CampaignTiming.WatchAt(epoch,plan.Inbound.DepartureUT)==4,"Return departure in third human watch");Yes(CampaignTiming.WatchAt(epoch,plan.Inbound.ArrivalUT)==4,"Return arrival in third human watch");}
        Yes(DunaRouteMath.Evaluate(Inputs(101000,100000),outbound,inbound)==null,"Reject insufficient propellant");
        Yes(DunaRouteMath.Evaluate(p,outbound,new TransferLeg(150*Day,210*Day,500,500))==null,"Reject burns confined to NPC watch");
        Yes(DunaRouteMath.Evaluate(p,outbound,new TransferLeg(95*Day,170*Day,500,500))==null,"Reject insufficient Duna stay");
        Yes(DunaRouteMath.Evaluate(p,outbound,new TransferLeg(750*Day,810*Day,500,500))==null,"Reject route outside campaign");
        var search=new DunaRouteSearch(Inputs(),Circular);int ticks=0,reported=0;
        foreach(var improvement in search.Scan()){ticks++;if(improvement!=null)reported++;Yes(ticks<=20000,"Search must yield bounded work, including failures");}
        Yes(search.Attempts==ticks,"Each iterator step performs at most one solve");Yes(search.Best!=null&&reported>0,"Physical circular ephemerides should yield a full round-trip candidate");
        Yes(search.Best.TotalDeltaV>0,"No fake zero-cost mission");
        CheckArc(search.Best.Outbound,true);CheckArc(search.Best.Inbound,false);
        foreach(var b in search.Best.Burns)Yes(b.DurationSeconds>0&&b.RemainingKg>=p.DryKg,"Physical fuel/burn envelope");
        var poor=new DunaRouteSearch(Inputs(101000,100000),Circular);foreach(var x in poor.Scan()){}
        Yes(poor.Best==null,"No fake route when actual propulsion envelope is insufficient");
        Console.WriteLine("PASS: "+checks+" Duna assertions; four-burn rocket mass/time, physical encounter epochs, full watch envelopes, insufficient fuel/stay/calendar refusal and bounded circular-ephemeris round-trip search plus independent RK4 endpoint/velocity checks. Model only, no stock SOI/flight qualification.");
    }
}
