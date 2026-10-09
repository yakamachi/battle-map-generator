using System.Diagnostics;
using System.Text.Json;

namespace BattleMapGenerator.Api.Tests.Infrastructure;

// Runs MapDeterminismTests.Emit_matrix_for_child_process in a separate `dotnet test` process against the
// assembly this run already built, and returns the grids that process wrote.
public static class ChildRun
{
    public static async Task<JsonDocument> EmitMatrixAsync()
    {
        if (Environment.GetEnvironmentVariable(MapDeterminismTests.EmitPathVariable) is not null)
        {
            throw new InvalidOperationException("A child run must not start another child run.");
        }

        var path = Path.Combine(Path.GetTempPath(), $"determinism-{Guid.NewGuid():N}.json");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = FindRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("api.Tests");
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--filter");
        start.ArgumentList.Add($"FullyQualifiedName~{nameof(MapDeterminismTests)}.{nameof(MapDeterminismTests.Emit_matrix_for_child_process)}");
        start.Environment[MapDeterminismTests.EmitPathVariable] = path;

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

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

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException($"No global.json above {AppContext.BaseDirectory}.");
    }
}
