using System;
using System.Reflection;

namespace MegaCrew
{
    /// <summary>
    /// Compile-stable bridge to the current MissionConsole duty query. Every call
    /// invokes the authoritative query again; no readiness result is cached.
    /// </summary>
    public static class DutyReadinessBridge
    {
        const string MethodName = "CanPerformDuty";

        /// <summary>
        /// Rechecks live local crew readiness. Call immediately before a normal-duty
        /// mutation. This grants no multiplayer authority, burn approval, or emergency-control gate.
        /// </summary>
        public static bool CanPerformDuty(string crewName, Guid vesselId, out string reason)
        {
            return CanPerformDuty(crewName, vesselId, out reason, typeof(MissionConsole));
        }

        // Type injection is an internal test seam. Production always binds the real MissionConsole.
        internal static bool CanPerformDuty(string crewName, Guid vesselId, out string reason, Type consoleType)
        {
            reason = String.Empty;
            if (String.IsNullOrWhiteSpace(crewName))
            {
                reason = "Select a named mission crew member before performing duty.";
                return false;
            }
            if (vesselId == Guid.Empty)
            {
                reason = "The active mission vessel identity is unavailable.";
                return false;
            }

            MethodInfo method;
            try
            {
                method = ResolveQuery(consoleType);
            }
            catch (Exception exception)
            {
                reason = "Crew-duty readiness API could not be inspected: " + SafeMessage(exception);
                return false;
            }
            if (method == null)
            {
                reason = "The mission console crew-duty readiness API is unavailable or has an incompatible signature.";
                return false;
            }

            object[] arguments = { crewName, vesselId, null };
            try
            {
                object returned = method.Invoke(null, arguments);
                if (!(returned is bool))
                {
                    reason = "The mission console returned an invalid crew-duty readiness result.";
                    return false;
                }

                string apiReason = arguments[2] as string;
                if (!(bool)returned)
                {
                    reason = UsefulReason(apiReason, "The mission console denied this duty; recheck crew status.");
                    return false;
                }

                // The production contract uses an empty reason for a positive result.
                // Treat a contradictory result as unavailable rather than authorizing it.
                if (!String.IsNullOrWhiteSpace(apiReason))
                {
                    reason = "The mission console returned inconsistent crew-duty readiness; recheck crew status.";
                    return false;
                }

                reason = String.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException : exception;
                reason = "Crew-duty readiness could not be verified: " + SafeMessage(cause);
                return false;
            }
        }

        static MethodInfo ResolveQuery(Type consoleType)
        {
            if (consoleType == null) return null;
            Type[] signature = { typeof(string), typeof(Guid), typeof(string).MakeByRefType() };
            MethodInfo method = consoleType.GetMethod(MethodName,
                BindingFlags.Public | BindingFlags.Static, null, signature, null);
            if (method == null || method.ReturnType != typeof(bool) || method.ContainsGenericParameters) return null;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 3 || !parameters[2].IsOut ||
                parameters[2].ParameterType != typeof(string).MakeByRefType())
                return null;
            return method;
        }

        static string UsefulReason(string reason, string fallback)
        {
            return String.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
        }

        static string SafeMessage(Exception exception)
        {
            string message = exception == null ? null : exception.Message;
            return String.IsNullOrWhiteSpace(message) ? "mission console query failed." : message.Trim();
        }
    }
}
