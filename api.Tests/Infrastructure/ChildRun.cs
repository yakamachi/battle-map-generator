using System.Diagnostics;
using System.Text.Json;

namespace BattleMapGenerator.Api.Tests.Infrastructure;

// Runs MapDeterminismTests.Emit_matrix_for_child_process in a separate `dotnet test` process against the
// assembly this run already built, and returns the grids that process wrote.
public static class ChildRun
{
    // Generous relative to the ~1s measured child run (Phase 1 change notes); a hang must fail this
    // fact fast with a clear message instead of stalling ci.yml's 20-minute job timeout.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static async Task<JsonDocument> EmitMatrixAsync()
    {
        if (Environment.GetEnvironmentVariable(MapDeterminismTests.EmitPathVariable) is not null)
        {
            throw new InvalidOperationException("A child run must not start another child run.");
        }

        var path = Path.Combine(Path.GetTempPath(), $"determinism-{Guid.NewGuid():N}.json");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoRoot.Find(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("api.Tests");
        // Must match ci.yml's `dotnet test api.Tests -c Release`: --no-build uses whatever
        // configuration's output already exists, and CI never builds Debug.
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("Release");
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--filter");
        start.ArgumentList.Add($"FullyQualifiedName~{nameof(MapDeterminismTests)}.{nameof(MapDeterminismTests.Emit_matrix_for_child_process)}");
        start.Environment[MapDeterminismTests.EmitPathVariable] = path;

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(Timeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException($"Child run did not exit within {Timeout}; killed.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Child run failed with exit code {process.ExitCode}.\n{await stdout}\n{await stderr}");
            }
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Child run exited cleanly but wrote no file at {path}.\n{await stdout}");
            }

            return JsonDocument.Parse(await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
