using System.Text.Json;
using BattleMapGenerator.Api.Maps;
using BattleMapGenerator.Api.Tests.Infrastructure;

namespace BattleMapGenerator.Api.Tests;

// Cross-process determinism: a grid generated in a separate `dotnet test` process must equal the grid
// generated here. The separate process is this same test, started by ChildRun with the variable below set.
public sealed class MapDeterminismTests
{
    public const string EmitPathVariable = "DETERMINISM_EMIT_PATH";

    private const int SeedCount = 200;

    // Seeds 1–200 for a skirmish and for a Huge boss, at the default room count.
    internal static IEnumerable<(string Key, uint Seed, MapParameters Parameters)> Matrix()
    {
        for (uint seed = 1; seed <= SeedCount; seed++)
        {
            yield return ($"{seed}-skirmish", seed, new MapParameters(MapSize.DefaultRoomCount, EncounterType.Skirmish, null));
            yield return ($"{seed}-huge", seed, new MapParameters(MapSize.DefaultRoomCount, EncounterType.Boss, BossSize.Huge));
        }
    }

    internal static JsonSerializerOptions Json()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        MapJson.Configure(options);
        return options;
    }

    // Inert in normal runs: it writes only when ChildRun starts this test in a separate process.
    [Fact]
    public void Emit_matrix_for_child_process()
    {
        var path = Environment.GetEnvironmentVariable(EmitPathVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var entries = Matrix().ToDictionary(entry => entry.Key, entry => BspGenerator.Generate(entry.Seed, entry.Parameters));
        File.WriteAllText(path, JsonSerializer.Serialize(entries, Json()));
    }

    // The harness itself: one entry per (seed, parameters) pair in the matrix.
    [Fact]
    public async Task Child_run_writes_one_entry_per_matrix_pair()
    {
        using var entries = await ChildRun.EmitMatrixAsync();

        Assert.Equal(Matrix().Count(), entries.RootElement.EnumerateObject().Count());
    }

    // Risk #2: a grid generated in a separate process must equal the grid generated here, for every
    // seed and parameter pair in the matrix. Same-process repetition (MapGeneratorTests) cannot see
    // the class of drift this guards against (for example runtime string-hash randomisation).
    [Fact]
    public async Task Cross_run_matches_this_process_for_every_matrix_pair()
    {
        using var childEntries = await ChildRun.EmitMatrixAsync();
        var options = Json();

        foreach (var (key, seed, parameters) in Matrix())
        {
            var map = BspGenerator.Generate(seed, parameters);
            using var thisProcess = JsonSerializer.SerializeToDocument(map, options);
            var childProcess = childEntries.RootElement.GetProperty(key);

            Assert.True(
                JsonElement.DeepEquals(thisProcess.RootElement.GetProperty("cells"), childProcess.GetProperty("cells")),
                $"{key}: cells differ between this process and the child process.");
            Assert.True(
                JsonElement.DeepEquals(thisProcess.RootElement.GetProperty("rooms"), childProcess.GetProperty("rooms")),
                $"{key}: rooms differ between this process and the child process.");
        }
    }
}
