using System;
using System.Collections.Generic;
using MegaCrew.Navigation;
using NavVector = MegaCrew.Navigation.Vector3d;
namespace MegaCrew.DunaMission
{
    // Planning estimates only: no nodes, throttle, resources, crew or KSP UT writes.
    public struct PlanetState
    {
        public readonly NavVector Position,Velocity;
        public PlanetState(NavVector position,NavVector velocity){Position=position;Velocity=velocity;}
    }
    public sealed class DunaSearchInputs
    {
        public readonly double NowUT,SunMu,KerbinMu,DunaMu,KerbinParkingRadius,DunaParkingRadius;
        public readonly double WetKg,DryKg,ThrustN,IspSeconds;
        public readonly double DepartureSpanDays,DepartureStepDays,MinTransferDays,MaxTransferDays,TransferStepDays,ReturnStepDays,MinimumStayDays;
        public readonly double AlignmentSeconds,VerificationSeconds,EncounterReserveSeconds;
        public DunaSearchInputs(double now,double sunMu,double kerbinMu,double dunaMu,double kerbinParkingRadius,double dunaParkingRadius,
            double wetKg,double dryKg,double thrustN,double ispSeconds,
            double departureSpanDays=360,double departureStepDays=10,double minTransferDays=50,double maxTransferDays=110,
            double transferStepDays=15,double returnStepDays=15,double minimumStayDays=30,
            double alignmentSeconds=3600,double verificationSeconds=3600,double encounterReserveSeconds=172800)
        {
            foreach(double x in new[]{now,sunMu,kerbinMu,dunaMu,kerbinParkingRadius,dunaParkingRadius,wetKg,dryKg,thrustN,ispSeconds,
                departureSpanDays,departureStepDays,minTransferDays,maxTransferDays,transferStepDays,returnStepDays,minimumStayDays,alignmentSeconds,verificationSeconds,encounterReserveSeconds})
                if(!CampaignTiming.Finite(x)||x<0)throw new ArgumentOutOfRangeException("inputs");
            if(sunMu<=0||kerbinMu<=0||dunaMu<=0||kerbinParkingRadius<=0||dunaParkingRadius<=0||dryKg<=0||wetKg<=dryKg||thrustN<=0||ispSeconds<=0||
                departureStepDays<=0||transferStepDays<=0||returnStepDays<=0||minTransferDays<=0||maxTransferDays<minTransferDays||
                departureSpanDays>720||maxTransferDays>360||departureStepDays<1||transferStepDays<1||returnStepDays<1||minimumStayDays<1||
                alignmentSeconds>86400||verificationSeconds>86400||encounterReserveSeconds<0||encounterReserveSeconds>10*86400)
                throw new ArgumentOutOfRangeException("inputs","Unsupported or unbounded search inputs");
            double count=(Math.Floor(departureSpanDays/departureStepDays)+1)*(Math.Floor((maxTransferDays-minTransferDays)/transferStepDays)+1);
            double returnCount=8*(Math.Floor(720/returnStepDays)+1)*(Math.Floor((maxTransferDays-minTransferDays)/transferStepDays)+1);
            if(count+returnCount>20000||!CampaignTiming.Finite(now+(departureSpanDays+1080)*86400)||now+(departureSpanDays+1080)*86400<=now)
                throw new ArgumentOutOfRangeException("inputs","Search exceeds its numerical/work budget");
            NowUT=now;SunMu=sunMu;KerbinMu=kerbinMu;DunaMu=dunaMu;KerbinParkingRadius=kerbinParkingRadius;DunaParkingRadius=dunaParkingRadius;
            WetKg=wetKg;DryKg=dryKg;ThrustN=thrustN;IspSeconds=ispSeconds;DepartureSpanDays=departureSpanDays;DepartureStepDays=departureStepDays;
            MinTransferDays=minTransferDays;MaxTransferDays=maxTransferDays;TransferStepDays=transferStepDays;ReturnStepDays=returnStepDays;MinimumStayDays=minimumStayDays;
            AlignmentSeconds=alignmentSeconds;VerificationSeconds=verificationSeconds;EncounterReserveSeconds=encounterReserveSeconds;
        }
    }
    public sealed class TransferLeg
    {
        public readonly double DepartureUT,ArrivalUT,DepartureExcess,ArrivalExcess;
        public TransferLeg(double departure,double arrival,double departureExcess,double arrivalExcess)
        {
            foreach(double n in new[]{departure,arrival,departureExcess,arrivalExcess})if(!CampaignTiming.Finite(n)||n<0)throw new ArgumentOutOfRangeException();
            if(arrival<=departure)throw new ArgumentOutOfRangeException();
            DepartureUT=departure;ArrivalUT=arrival;DepartureExcess=departureExcess;ArrivalExcess=arrivalExcess;
        }
    }
    public sealed class DunaBurnEstimate
    {
        public readonly string Name;
        // Planet-centre encounter epoch, NOT an executable ignition time.
        public readonly double EncounterUT,DeltaV,DurationSeconds,RemainingKg;
        public DunaBurnEstimate(string name,double ut,double dv,double duration,double remaining)
        {Name=name;EncounterUT=ut;DeltaV=dv;DurationSeconds=duration;RemainingKg=remaining;}
    }
    public sealed class DunaRoutePlan
    {
        readonly System.Collections.ObjectModel.ReadOnlyCollection<DunaBurnEstimate> burns;
        readonly System.Collections.ObjectModel.ReadOnlyCollection<CampaignEpochWindow> windows;
        public readonly TransferLeg Outbound,Inbound;
        public readonly double TotalDeltaV,RemainingKg,EstimatedExtraPlaySeconds;
        public System.Collections.ObjectModel.ReadOnlyCollection<DunaBurnEstimate> Burns{get{return burns;}}
        public System.Collections.ObjectModel.ReadOnlyCollection<CampaignEpochWindow> EpochWindows{get{return windows;}}
        internal DunaRoutePlan(TransferLeg outbound,TransferLeg inbound,List<DunaBurnEstimate> source,List<CampaignEpochWindow> epochs,double dv,double remaining,double extra)
        {Outbound=outbound;Inbound=inbound;burns=new List<DunaBurnEstimate>(source).AsReadOnly();windows=new List<CampaignEpochWindow>(epochs).AsReadOnly();TotalDeltaV=dv;RemainingKg=remaining;EstimatedExtraPlaySeconds=extra;}
    }
    public static class DunaRouteMath
    {
        const double G0=9.80665;
        public static double ParkingDeltaV(double excess,double mu,double radius)
        {
            if(!CampaignTiming.Finite(excess)||!CampaignTiming.Finite(mu)||!CampaignTiming.Finite(radius)||excess<0||mu<=0||radius<=0)throw new ArgumentOutOfRangeException();
            double circular=Math.Sqrt(mu/radius),hyperbolic=Math.Sqrt(excess*excess+2*mu/radius);
            double dv=hyperbolic-circular;if(!CampaignTiming.Finite(dv)||dv<=0)throw new ArgumentOutOfRangeException();return dv;
        }
        public static DunaRoutePlan Evaluate(DunaSearchInputs p,TransferLeg outward,TransferLeg inward)
        {
            if(p==null||outward==null||inward==null)throw new ArgumentNullException();
            if(outward.DepartureUT<=p.NowUT||inward.DepartureUT<outward.ArrivalUT+p.MinimumStayDays*86400)return null;
            double[] times={outward.DepartureUT,outward.ArrivalUT,inward.DepartureUT,inward.ArrivalUT};
            double[] costs={ParkingDeltaV(outward.DepartureExcess,p.KerbinMu,p.KerbinParkingRadius),ParkingDeltaV(outward.ArrivalExcess,p.DunaMu,p.DunaParkingRadius),
                ParkingDeltaV(inward.DepartureExcess,p.DunaMu,p.DunaParkingRadius),ParkingDeltaV(inward.ArrivalExcess,p.KerbinMu,p.KerbinParkingRadius)};
            string[] names={"Kerbin departure","Duna capture","Duna departure","Kerbin orbital capture"};
            var burns=new List<DunaBurnEstimate>();var duty=new List<BurnDutyInterval>();double mass=p.WetKg,total=0,extra=0;
            double exhaust=p.IspSeconds*G0,massRate=p.ThrustN/exhaust;
            if(!CampaignTiming.Finite(exhaust)||!CampaignTiming.Finite(massRate)||massRate<=0)return null;
            for(int i=0;i<4;i++)
            {
                double after=mass*Math.Exp(-costs[i]/exhaust),seconds=(mass-after)/massRate;
                if(!CampaignTiming.Finite(after)||after<p.DryKg||!CampaignTiming.Finite(seconds)||seconds<=0||seconds>86400)return null;
                burns.Add(new DunaBurnEstimate(names[i],times[i],costs[i],seconds,after));
                // Symmetric timing reserve is a preliminary planning margin, NOT
                // proof that a future fitted SOI trajectory or finite burn fits.
                double pre=p.AlignmentSeconds+p.EncounterReserveSeconds+seconds/2;
                double post=p.VerificationSeconds+p.EncounterReserveSeconds;
                if(times[i]<pre)return null;
                duty.Add(new BurnDutyInterval(times[i],seconds/2,pre,post));
                mass=after;total+=costs[i];extra+=CampaignTiming.ExtraBurnPlaySeconds(seconds);
            }
            // Do not permit overlapping capture/departure duty envelopes.
            for(int i=1;i<duty.Count;i++)if(duty[i-1].EndUT>duty[i].StartUT)return null;
            double latest=duty[0].StartUT;
            if(latest<p.NowUT)return null;
            var epochs=CampaignEpochPlanner.Find(p.NowUT,latest,duty);
            // Return during the third human watch, leaving the middle human
            // watch for Duna surface/science work. Intersect existing ranges;
            // never edit any physical encounter epoch to make this true.
            var returnEpochs=new List<CampaignEpochWindow>();
            foreach(var e in epochs)
            {
                double first=Math.Max(e.EarliestUT,duty[3].EndUT-5*CampaignTiming.WatchSeconds);
                double last=Math.Min(e.LatestUT,duty[2].StartUT-4*CampaignTiming.WatchSeconds);
                if(first<=last)returnEpochs.Add(new CampaignEpochWindow(first,last));
            }
            epochs=returnEpochs;
            if(epochs.Count==0)return null;
            return new DunaRoutePlan(outward,inward,burns,epochs,total,mass,extra);
        }
    }
    public sealed class DunaRouteSearch
    {
        readonly DunaSearchInputs p;readonly Func<bool,double,PlanetState> ephemeris;bool started;
        public int Attempts{get;private set;}public int SolvedLegs{get;private set;}public int FeasiblePlans{get;private set;}
        public DunaRoutePlan Best{get;private set;}
        public DunaRouteSearch(DunaSearchInputs inputs,Func<bool,double,PlanetState> states)
        {if(inputs==null||states==null)throw new ArgumentNullException();p=inputs;ephemeris=states;}
        static NavVector Cross(NavVector a,NavVector b){return new NavVector(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        TransferLeg Leg(bool outbound,double departure,double arrival)
        {
            Attempts++;
            var from=ephemeris(!outbound,departure);var to=ephemeris(outbound,arrival);
            LambertResult result;
            try{result=Lambert.SolveShortWay(from.Position,to.Position,arrival-departure,p.SunMu);}
            catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
            if(Math.Abs(result.TimeResidual)>Math.Max(.01,(arrival-departure)*1e-9))return null;
            // Reject retrograde solutions rather than relabelling a short-way arc.
            if(NavVector.Dot(Cross(from.Position,result.DepartureVelocity),Cross(from.Position,from.Velocity))<=0)return null;
            var leg=new TransferLeg(departure,arrival,(result.DepartureVelocity-from.Velocity).Magnitude,(result.ArrivalVelocity-to.Velocity).Magnitude);
            SolvedLegs++;return leg;
        }
        public IEnumerable<DunaRoutePlan> Scan()
        {
            if(started)throw new InvalidOperationException("Create a new search for each snapshot");
            started=true;
            var departures=new List<TransferLeg>();
            int nd=(int)Math.Floor(p.DepartureSpanDays/p.DepartureStepDays),nt=(int)Math.Floor((p.MaxTransferDays-p.MinTransferDays)/p.TransferStepDays);
            for(int d=0;d<=nd;d++)for(int t=0;t<=nt;t++)
            {
                double start=p.NowUT+p.AlignmentSeconds+p.EncounterReserveSeconds+86400+d*p.DepartureStepDays*86400,end=start+(p.MinTransferDays+t*p.TransferStepDays)*86400;
                var leg=Leg(true,start,end);
                if(leg!=null)
                {
                    departures.Add(leg);departures.Sort((a,b)=>(a.DepartureExcess+a.ArrivalExcess).CompareTo(b.DepartureExcess+b.ArrivalExcess));
                    if(departures.Count>8)departures.RemoveAt(8);
                }
                yield return null; // bounded work per UI update, even with no solution
            }
            foreach(var outward in departures)
            {
                double earliest=outward.ArrivalUT+p.MinimumStayDays*86400;
                double latest=outward.DepartureUT+CampaignTiming.MissionSeconds;
                for(int d=0;earliest+d*p.ReturnStepDays*86400<latest;d++)for(int t=0;t<=nt;t++)
                {
                    double start=earliest+d*p.ReturnStepDays*86400,end=start+(p.MinTransferDays+t*p.TransferStepDays)*86400;
                    var inward=Leg(false,start,end);DunaRoutePlan plan=inward==null?null:DunaRouteMath.Evaluate(p,outward,inward);
                    if(plan!=null){FeasiblePlans++;if(Best==null||plan.TotalDeltaV<Best.TotalDeltaV)Best=plan;}
                    yield return plan;
                }
            }
        }
    }
}
