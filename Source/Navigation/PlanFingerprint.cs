using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace MegaCrew.Navigation
{
    public static class PlanFingerprint
    {
        // The caller must additionally include and observe every mission-specific constraint.
        public static string Compute(string vessel,string node,string body,string target,string frame,double ut,double radial,double normal,double prograde)
        {
            if(string.IsNullOrWhiteSpace(vessel)||string.IsNullOrWhiteSpace(node)||string.IsNullOrWhiteSpace(body)||string.IsNullOrWhiteSpace(frame)||target==null)throw new ArgumentException("Missing plan identity");
            foreach(var x in new[]{ut,radial,normal,prograde})if(double.IsNaN(x)||double.IsInfinity(x))throw new ArgumentException("Nonfinite plan value");
            Func<string,string> encode=s=>Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
            var c=CultureInfo.InvariantCulture;
            var data=encode(vessel)+"|"+encode(node)+"|"+encode(body)+"|"+encode(target)+"|"+encode(frame)+"|"+ut.ToString("R",c)+"|"+radial.ToString("R",c)+"|"+normal.ToString("R",c)+"|"+prograde.ToString("R",c);
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-","").ToLowerInvariant();
        }
    }
}
