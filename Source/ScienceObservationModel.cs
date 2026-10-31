using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MegaCrew
{
    /// <summary>One stored-data card observation; units are retained science-data units.</summary>
    public readonly struct ScienceDataObservation
    {
        public string SubjectId { get; }
        public double DataUnits { get; }

        public ScienceDataObservation(string subjectId, double dataUnits)
        {
            ValidateSubjectId(subjectId);
            RequireFiniteNonnegative(dataUnits, "dataUnits");
            SubjectId = subjectId;
            DataUnits = dataUnits;
        }

        internal static void ValidateSubjectId(string subjectId)
        {
            if (String.IsNullOrWhiteSpace(subjectId))
                throw new ArgumentOutOfRangeException("subjectId", "Subject ID must not be null, empty, or whitespace.");
        }

        internal static void RequireFiniteNonnegative(double value, string name)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(name, "Stored data units must be finite and nonnegative.");
        }

        internal static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }

    /// <summary>Immutable aggregate retained-data amount for one exact subject ID.</summary>
    public sealed class ScienceSubjectAmount
    {
        public string SubjectId { get; private set; }
        public double DataUnits { get; private set; }

        internal ScienceSubjectAmount(string subjectId, double dataUnits)
        {
            SubjectId = subjectId;
            DataUnits = dataUnits;
        }
    }

    /// <summary>An immutable, copied, ordinal-sorted observation of stored subject data.</summary>
    public sealed class ScienceObservationSnapshot
    {
        readonly SortedDictionary<string, double> amounts;
        readonly ReadOnlyCollection<ScienceSubjectAmount> subjects;
        public ReadOnlyCollection<ScienceSubjectAmount> Subjects { get { return subjects; } }
        public double TotalDataUnits { get; private set; }

        ScienceObservationSnapshot(SortedDictionary<string, double> aggregate, double total)
        {
            amounts = aggregate;
            TotalDataUnits = total;
            var items = new List<ScienceSubjectAmount>(aggregate.Count);
            foreach (KeyValuePair<string, double> entry in aggregate)
                items.Add(new ScienceSubjectAmount(entry.Key, entry.Value));
            subjects = items.AsReadOnly();
        }

        /// <summary>
        /// Copies and aggregates all cards with the same exact, case-sensitive subject ID.
        /// Duplicate science cards remain one subject total; snapshots do not retain input objects.
        /// </summary>
        public static ScienceObservationSnapshot Create(IEnumerable<ScienceDataObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException("observations");
            var cardsBySubject = new SortedDictionary<string, List<double>>(StringComparer.Ordinal);
            foreach (ScienceDataObservation observation in observations)
            {
                ScienceDataObservation.ValidateSubjectId(observation.SubjectId);
                ScienceDataObservation.RequireFiniteNonnegative(observation.DataUnits, "dataUnits");
                List<double> cards;
                if (!cardsBySubject.TryGetValue(observation.SubjectId, out cards))
                {
                    cards = new List<double>();
                    cardsBySubject.Add(observation.SubjectId, cards);
                }
                cards.Add(observation.DataUnits);
            }

            // Sort card amounts before summing so container/list enumeration order cannot
            // manufacture a measured delta for the same multiset after an internal transfer.
            var aggregate = new SortedDictionary<string, double>(StringComparer.Ordinal);
            double total = 0.0;
            double totalCompensation = 0.0;
            foreach (KeyValuePair<string, List<double>> entry in cardsBySubject)
            {
                entry.Value.Sort();
                double subjectTotal = ScienceObservationAggregation.SumFinite(entry.Value, "aggregated subject data");
                aggregate.Add(entry.Key, subjectTotal);
                ScienceObservationAggregation.AddCompensated(ref total, ref totalCompensation, subjectTotal, "total stored data");
            }
            return new ScienceObservationSnapshot(aggregate, total);
        }

        internal bool TryGetDataUnits(string subjectId, out double dataUnits)
        {
            return amounts.TryGetValue(subjectId, out dataUnits);
        }
    }

    public enum ScienceChangeKind
    {
        Added,
        Increased,
        Removed,
        Decreased,
        Unchanged
    }

    internal static class ScienceObservationAggregation
    {
        internal static double SumFinite(List<double> values, string name)
        {
            double sum = 0.0;
            double compensation = 0.0;
            foreach (double value in values)
                AddCompensated(ref sum, ref compensation, value, name);
            return sum;
        }

        internal static void AddCompensated(ref double sum, ref double compensation, double value, string name)
        {
            double corrected = value - compensation;
            double next = sum + corrected;
            if (!ScienceDataObservation.IsFinite(next))
                throw new ArgumentOutOfRangeException(name, "Aggregated stored data overflowed.");
            compensation = (next - sum) - corrected;
            if (!ScienceDataObservation.IsFinite(compensation))
                throw new ArgumentOutOfRangeException(name, "Aggregation compensation overflowed.");
            sum = next;
        }
    }

    /// <summary>Immutable measured delta for one stored-data subject.</summary>
    public sealed class ScienceSubjectDelta
    {
        public string SubjectId { get; private set; }
        public double BeforeDataUnits { get; private set; }
        public double AfterDataUnits { get; private set; }
        /// <summary>Signed measured difference after minus before, including tolerated noise.</summary>
        public double DeltaDataUnits { get; private set; }
        public ScienceChangeKind Change { get; private set; }
        public ScienceChangeKind ChangeKind { get { return Change; } }

        internal ScienceSubjectDelta(string subjectId, double before, double after,
            double delta, ScienceChangeKind change)
        {
            SubjectId = subjectId;
            BeforeDataUnits = before;
            AfterDataUnits = after;
            DeltaDataUnits = delta;
            Change = change;
        }
    }

    /// <summary>
    /// Immutable comparison of retained data between two observations. It measures
    /// stored-data changes only, not science points, discoveries or experiment success.
    /// </summary>
    public sealed class ScienceObservationComparison
    {
        readonly ReadOnlyCollection<ScienceSubjectDelta> subjects;
        public ReadOnlyCollection<ScienceSubjectDelta> Subjects { get { return subjects; } }
        public double BeforeTotalDataUnits { get; private set; }
        public double AfterTotalDataUnits { get; private set; }
        /// <summary>Raw after-total minus before-total; tiny tolerated deltas are still included.</summary>
        public double NetChangeDataUnits { get; private set; }
        public double AddedDataUnits { get; private set; }
        public double IncreasedDataUnits { get; private set; }
        public double RemovedDataUnits { get; private set; }
        public double DecreasedDataUnits { get; private set; }
        public double UnchangedDataUnits { get; private set; }
        public int AddedSubjectCount { get; private set; }
        public int IncreasedSubjectCount { get; private set; }
        public int RemovedSubjectCount { get; private set; }
        public int DecreasedSubjectCount { get; private set; }
        public int UnchangedSubjectCount { get; private set; }

        internal ScienceObservationComparison(List<ScienceSubjectDelta> changes,
            double beforeTotal, double afterTotal, double added, double increased,
            double removed, double decreased, double unchanged, int addedCount,
            int increasedCount, int removedCount, int decreasedCount, int unchangedCount)
        {
            subjects = changes.AsReadOnly();
            BeforeTotalDataUnits = beforeTotal;
            AfterTotalDataUnits = afterTotal;
            NetChangeDataUnits = afterTotal - beforeTotal;
            AddedDataUnits = added;
            IncreasedDataUnits = increased;
            RemovedDataUnits = removed;
            DecreasedDataUnits = decreased;
            UnchangedDataUnits = unchanged;
            AddedSubjectCount = addedCount;
            IncreasedSubjectCount = increasedCount;
            RemovedSubjectCount = removedCount;
            DecreasedSubjectCount = decreasedCount;
            UnchangedSubjectCount = unchangedCount;
        }
    }

    /// <summary>Compares aggregate retained data by exact subject ID without awarding science.</summary>
    public static class ScienceObservationModel
    {
        /// <param name="toleranceDataUnits">
        /// Nonnegative absolute tolerance used only for classifying same-present-subject
        /// deltas as unchanged. Defaults to zero. Raw signed deltas and net total always
        /// remain visible, even when within tolerance.
        /// </param>
        public static ScienceObservationComparison Compare(ScienceObservationSnapshot before,
            ScienceObservationSnapshot after, double toleranceDataUnits = 0.0)
        {
            if (before == null) throw new ArgumentNullException("before");
            if (after == null) throw new ArgumentNullException("after");
            ScienceDataObservation.RequireFiniteNonnegative(toleranceDataUnits, "toleranceDataUnits");

            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (ScienceSubjectAmount item in before.Subjects) ids.Add(item.SubjectId);
            foreach (ScienceSubjectAmount item in after.Subjects) ids.Add(item.SubjectId);

            var changes = new List<ScienceSubjectDelta>(ids.Count);
            double added = 0, increased = 0, removed = 0, decreased = 0, unchanged = 0;
            double addedComp = 0, increasedComp = 0, removedComp = 0, decreasedComp = 0, unchangedComp = 0;
            int addedCount = 0, increasedCount = 0, removedCount = 0, decreasedCount = 0, unchangedCount = 0;
            foreach (string id in ids)
            {
                double oldValue, newValue;
                bool hadBefore = before.TryGetDataUnits(id, out oldValue);
                bool hasAfter = after.TryGetDataUnits(id, out newValue);
                if (!hadBefore) oldValue = 0.0;
                if (!hasAfter) newValue = 0.0;
                double delta = newValue - oldValue;
                if (!ScienceDataObservation.IsFinite(delta))
                    throw new ArgumentOutOfRangeException("snapshots", "Subject delta overflowed.");

                ScienceChangeKind kind;
                if (!hadBefore && hasAfter)
                {
                    kind = ScienceChangeKind.Added;
                    ScienceObservationAggregation.AddCompensated(ref added, ref addedComp, newValue, "added");
                    addedCount++;
                }
                else if (hadBefore && !hasAfter)
                {
                    kind = ScienceChangeKind.Removed;
                    ScienceObservationAggregation.AddCompensated(ref removed, ref removedComp, oldValue, "removed");
                    removedCount++;
                }
                else if (delta > toleranceDataUnits)
                {
                    kind = ScienceChangeKind.Increased;
                    ScienceObservationAggregation.AddCompensated(ref increased, ref increasedComp, delta, "increased");
                    increasedCount++;
                }
                else if (delta < -toleranceDataUnits)
                {
                    kind = ScienceChangeKind.Decreased;
                    ScienceObservationAggregation.AddCompensated(ref decreased, ref decreasedComp, -delta, "decreased");
                    decreasedCount++;
                }
                else
                {
                    kind = ScienceChangeKind.Unchanged;
                    ScienceObservationAggregation.AddCompensated(ref unchanged, ref unchangedComp, newValue, "unchanged");
                    unchangedCount++;
                }
                changes.Add(new ScienceSubjectDelta(id, oldValue, newValue, delta, kind));
            }

            return new ScienceObservationComparison(changes, before.TotalDataUnits,
                after.TotalDataUnits, added, increased, removed, decreased, unchanged,
                addedCount, increasedCount, removedCount, decreasedCount, unchangedCount);
        }

    }
}
