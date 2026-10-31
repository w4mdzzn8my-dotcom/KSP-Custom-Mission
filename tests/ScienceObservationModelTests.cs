using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MegaCrew;

class ScienceObservationModelTests
{
    static void Is(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
    }

    static void Near(double actual, double expected, double tolerance, string name)
    {
        if (Double.IsNaN(actual) || Double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
            throw new Exception(name + ": expected " + expected + " +/- " + tolerance + ", got " + actual);
    }

    static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception(name + ": expected " + typeof(T).Name);
    }

    static ScienceSubjectDelta Find(ScienceObservationComparison result, string id)
    {
        foreach (ScienceSubjectDelta item in result.Subjects)
            if (String.Equals(item.SubjectId, id, StringComparison.Ordinal)) return item;
        throw new Exception("Missing subject delta: " + id);
    }

    static void Main()
    {
        // Moving the same retained data between containers/cards leaves each subject aggregate unchanged.
        var transferBefore = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("crewReport@KerbinInSpaceLow", 2.0),
            new ScienceDataObservation("crewReport@KerbinInSpaceLow", 3.0),
            new ScienceDataObservation("evaReport@KerbinSrfLanded", 1.5)
        });
        var transferAfter = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("crewReport@KerbinInSpaceLow", 4.0),
            new ScienceDataObservation("crewReport@KerbinInSpaceLow", 1.0),
            new ScienceDataObservation("evaReport@KerbinSrfLanded", 1.5)
        });
        var transfer = ScienceObservationModel.Compare(transferBefore, transferAfter);
        Near(transferBefore.TotalDataUnits, 6.5, 0, "transfer baseline total");
        Near(transferAfter.TotalDataUnits, 6.5, 0, "transfer current total");
        Near(transfer.NetChangeDataUnits, 0, 0, "transfer net delta");
        Is(transfer.Subjects.Count == 2 && transfer.UnchangedSubjectCount == 2,
            "duplicate cards aggregate to two unchanged subject rows");
        Near(Find(transfer, "crewReport@KerbinInSpaceLow").BeforeDataUnits, 5, 0, "duplicate baseline aggregate");
        Near(Find(transfer, "crewReport@KerbinInSpaceLow").AfterDataUnits, 5, 0, "duplicate current aggregate");

        var before = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("same", 4),
            new ScienceDataObservation("transfer", 2), new ScienceDataObservation("transfer", 3),
            new ScienceDataObservation("increase", 2),
            new ScienceDataObservation("decrease", 9),
            new ScienceDataObservation("remove", 7),
            new ScienceDataObservation("mixedCase", 1)
        });
        var after = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("same", 4),
            new ScienceDataObservation("transfer", 4), new ScienceDataObservation("transfer", 1),
            new ScienceDataObservation("increase", 5),
            new ScienceDataObservation("decrease", 4),
            new ScienceDataObservation("added", 6),
            new ScienceDataObservation("MixedCase", 3)
        });
        var mixed = ScienceObservationModel.Compare(before, after);
        Is(Find(mixed, "same").ChangeKind == ScienceChangeKind.Unchanged, "same subject unchanged");
        Is(Find(mixed, "transfer").Change == ScienceChangeKind.Unchanged, "duplicate transfer unchanged");
        Is(Find(mixed, "increase").Change == ScienceChangeKind.Increased, "subject increased");
        Near(Find(mixed, "increase").DeltaDataUnits, 3, 0, "increased delta");
        Is(Find(mixed, "decrease").Change == ScienceChangeKind.Decreased, "subject decreased");
        Near(Find(mixed, "decrease").DeltaDataUnits, -5, 0, "decreased signed delta");
        Is(Find(mixed, "remove").Change == ScienceChangeKind.Removed, "subject removed");
        Near(Find(mixed, "remove").DeltaDataUnits, -7, 0, "removed signed delta");
        Is(Find(mixed, "added").Change == ScienceChangeKind.Added, "subject added");
        Near(Find(mixed, "added").DeltaDataUnits, 6, 0, "added delta");
        Is(Find(mixed, "mixedCase").Change == ScienceChangeKind.Removed &&
            Find(mixed, "MixedCase").Change == ScienceChangeKind.Added,
            "subject identity is case-sensitive ordinal");
        Is(mixed.AddedSubjectCount == 2 && mixed.IncreasedSubjectCount == 1 &&
            mixed.RemovedSubjectCount == 2 && mixed.DecreasedSubjectCount == 1 && mixed.UnchangedSubjectCount == 2,
            "all change category counts");
        Near(mixed.AddedDataUnits, 9, 0, "added units total");
        Near(mixed.IncreasedDataUnits, 3, 0, "increased units total");
        Near(mixed.RemovedDataUnits, 8, 0, "removed units total");
        Near(mixed.DecreasedDataUnits, 5, 0, "decreased units total");
        Near(mixed.UnchangedDataUnits, 9, 0, "unchanged current units total");
        Near(mixed.BeforeTotalDataUnits, 28, 0, "mixed baseline total");
        Near(mixed.AfterTotalDataUnits, 27, 0, "mixed current total");
        Near(mixed.NetChangeDataUnits, -1, 0, "mixed net measured change");

        var zeroBaseline = ScienceObservationSnapshot.Create(new[] { new ScienceDataObservation("zeroSubject", 0) });
        var zeroIncrease = ScienceObservationModel.Compare(zeroBaseline,
            ScienceObservationSnapshot.Create(new[] { new ScienceDataObservation("zeroSubject", 2) }));
        Is(Find(zeroIncrease, "zeroSubject").Change == ScienceChangeKind.Increased,
            "present zero-data subject growing is an increase, not an addition");

        var zeroAdded = ScienceObservationModel.Compare(ScienceObservationSnapshot.Create(new ScienceDataObservation[0]),
            ScienceObservationSnapshot.Create(new[] { new ScienceDataObservation("zeroCard", 0) }));
        Is(Find(zeroAdded, "zeroCard").Change == ScienceChangeKind.Added && zeroAdded.AddedSubjectCount == 1,
            "new zero-data card remains a presence addition with zero units");
        Near(zeroAdded.AddedDataUnits, 0, 0, "zero-data addition units");

        // Optional tolerance classifies small same-present-subject noise as unchanged,
        // while preserving the raw measured delta and totals.
        var orderA = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("large", 1e16), new ScienceDataObservation("large", 1), new ScienceDataObservation("large", 1)
        });
        var orderB = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("large", 1), new ScienceDataObservation("large", 1e16), new ScienceDataObservation("large", 1)
        });
        var orderC = ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("large", 1), new ScienceDataObservation("large", 1), new ScienceDataObservation("large", 1e16)
        });
        Near(orderA.Subjects[0].DataUnits, orderB.Subjects[0].DataUnits, 0, "aggregate independent of card enumeration order");
        Near(orderA.Subjects[0].DataUnits, orderC.Subjects[0].DataUnits, 0, "aggregate independent of reverse card enumeration order");
        Is(ScienceObservationModel.Compare(orderA, orderC).Subjects[0].Change == ScienceChangeKind.Unchanged,
            "reordered identical report multiset does not fabricate progress");

        var noisy = ScienceObservationModel.Compare(
            ScienceObservationSnapshot.Create(new[] { new ScienceDataObservation("same", 10.0) }),
            ScienceObservationSnapshot.Create(new[] { new ScienceDataObservation("same", 10.0001) }), 0.001);
        Is(Find(noisy, "same").Change == ScienceChangeKind.Unchanged, "tolerated small delta is classified unchanged");
        Near(Find(noisy, "same").DeltaDataUnits, 0.0001, 1e-12, "tolerated raw delta preserved");
        Near(noisy.NetChangeDataUnits, 0.0001, 1e-12, "tolerated net delta preserved");

        // Snapshot owns an immutable copy of the caller's records and exposes read-only collections.
        var mutableInput = new List<ScienceDataObservation> { new ScienceDataObservation("copy", 2) };
        var copied = ScienceObservationSnapshot.Create(mutableInput);
        mutableInput.Add(new ScienceDataObservation("later", 100));
        Is(copied.Subjects.Count == 1 && copied.Subjects[0].SubjectId == "copy", "snapshot copied source sequence");
        Throws<NotSupportedException>(() => ((IList<ScienceSubjectAmount>)copied.Subjects).Add(new ScienceSubjectAmount("mutate", 1)), "snapshot collection is read-only");
        Throws<NotSupportedException>(() => ((IList<ScienceSubjectDelta>)mixed.Subjects).Add(new ScienceSubjectDelta("mutate", 0, 0, 0, ScienceChangeKind.Unchanged)), "comparison collection is read-only");
        string[] expectedOrder = { "MixedCase", "added", "decrease", "increase", "mixedCase", "remove", "same", "transfer" };
        Is(mixed.Subjects.Count == expectedOrder.Length, "comparison subject count");
        for (int i = 0; i < expectedOrder.Length; i++)
            Is(mixed.Subjects[i].SubjectId == expectedOrder[i], "ordinal subject order at " + i);

        Throws<ArgumentOutOfRangeException>(() => new ScienceDataObservation(null, 1), "null subject ID");
        Throws<ArgumentOutOfRangeException>(() => new ScienceDataObservation("", 1), "empty subject ID");
        Throws<ArgumentOutOfRangeException>(() => new ScienceDataObservation("   ", 1), "whitespace subject ID");
        Is(new ScienceDataObservation("crewReport@MunFlyingHigh", 1).SubjectId == "crewReport@MunFlyingHigh",
            "nonnumeric KSP subject IDs are valid");
        Throws<ArgumentOutOfRangeException>(() => new ScienceDataObservation("x", -1), "negative units");
        Throws<ArgumentOutOfRangeException>(() => new ScienceDataObservation("x", Double.NaN), "NaN units");
        Throws<ArgumentOutOfRangeException>(() => new ScienceDataObservation("x", Double.PositiveInfinity), "infinite units");
        Throws<ArgumentOutOfRangeException>(() => ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("overflow", Double.MaxValue), new ScienceDataObservation("overflow", Double.MaxValue)
        }), "duplicate-subject aggregate overflow");
        Throws<ArgumentOutOfRangeException>(() => ScienceObservationSnapshot.Create(new[] {
            new ScienceDataObservation("one", Double.MaxValue), new ScienceDataObservation("two", Double.MaxValue)
        }), "snapshot total overflow");
        Throws<ArgumentOutOfRangeException>(() => ScienceObservationSnapshot.Create(new[] { default(ScienceDataObservation) }),
            "default observation rejected");
        Throws<ArgumentNullException>(() => ScienceObservationSnapshot.Create(null), "null observation sequence");
        Throws<ArgumentOutOfRangeException>(() => ScienceObservationModel.Compare(before, after, -0.1), "negative tolerance");
        Throws<ArgumentOutOfRangeException>(() => ScienceObservationModel.Compare(before, after, Double.NaN), "NaN tolerance");
        Throws<ArgumentNullException>(() => ScienceObservationModel.Compare(null, after), "null baseline");
        Throws<ArgumentNullException>(() => ScienceObservationModel.Compare(before, null), "null current snapshot");

        Console.WriteLine("PASS: vessel-wide duplicate aggregation/transfers, order-independent totals, per-subject changes, case-sensitive IDs, zero-data presence, totals, tolerance, immutability, and invalid/overflow rejection.");
    }
}
