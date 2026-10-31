using System;
namespace MegaCrew
{
    public static class CryoPowerModel
    {
        static void Valid(double x){if(double.IsNaN(x)||double.IsInfinity(x)||x<0)throw new ArgumentOutOfRangeException();}
        public static double PodLoad(double ecPerMinute,int frozen)
        {
            Valid(ecPerMinute);if(frozen<0)throw new ArgumentOutOfRangeException();
            double load=ecPerMinute/60*frozen;Valid(load);return load;
        }
        public static double TimeToReserve(double stored,double reserve,double maximumLoad,double guaranteedGeneration)
        {
            Valid(stored);Valid(reserve);Valid(maximumLoad);Valid(guaranteedGeneration);
            if(stored<=reserve)return 0;
            double drain=maximumLoad-guaranteedGeneration;
            if(drain<=0)return double.PositiveInfinity;
            double seconds=(stored-reserve)/drain;if(double.IsInfinity(seconds))throw new ArgumentOutOfRangeException();return seconds;
        }
        public static double StoredAfter(double stored,double capacity,double load,double generation,double seconds)
        {
            Valid(stored);Valid(capacity);Valid(load);Valid(generation);Valid(seconds);
            if(stored>capacity)throw new ArgumentOutOfRangeException();
            double delta=(generation-load)*seconds;
            if(double.IsNaN(delta)||double.IsInfinity(delta))throw new ArgumentOutOfRangeException();
            return Math.Max(0,Math.Min(capacity,stored+delta));
        }
    }
}
