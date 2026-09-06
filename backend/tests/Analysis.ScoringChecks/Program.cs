using System.Text.Json;
using Analysis.Domain.Scoring;
using Analysis.ScoringChecks;

try
{
    if (args is ["--snapshot"])
    { Console.WriteLine(JsonSerializer.Serialize(await DatabaseChecks.SnapshotAsync())); return 0; }
    if (args is ["--hold-lock"])
    { await DatabaseChecks.HoldLockAsync(); return 0; }
    UnitChecks.Run();
    if (args is ["--database-checks"]) await DatabaseChecks.RunAsync();
    else if (args.Length != 0) throw new ArgumentException("Unsupported check command.");
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "m3-checks", assertions = Check.Count,
        manifestHash = ScoringModel.Slice1.Hash, sourceHash = ScoringModel.Slice1.SourceHash }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"FAIL {error.GetType().Name}: {error.Message}");
    return 1;
}
