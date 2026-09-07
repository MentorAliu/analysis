using System.Text.Json;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Analysis.ForwardChecks;

if (args is ["--database-checks"]) await DatabaseChecks.RunAsync();
else if (args is ["--hold-issuance-lock"]) await DatabaseChecks.HoldIssuanceLockAsync();
else if (args is ["--check-signal-cancellation"]) await DatabaseChecks.CheckSignalCancellationAsync();
else if (args is ["--snapshot"])
{
    Console.WriteLine(JsonSerializer.Serialize(new { snapshot = await DatabaseChecks.SnapshotAsync() }));
    return;
}
else if (args.Length == 0) { UnitChecks.Run(); await CollectionChecks.RunAsync(); }
else throw new ArgumentException("Unknown forward verification command.");
Console.WriteLine(JsonSerializer.Serialize(new { assertions = Check.Count, manifestHash = ScoringModel.Slice1.Hash,
    sourceHash = ScoringModel.Slice1.SourceHash, methodologyHash = ForwardMethodology.Hash, methodologySourceHash = ForwardMethodology.SourceHash }));
