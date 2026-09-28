using System.Text.Json;
using battle_map_generator_api.Maps;

namespace battle_map_generator_api.Tests;

// The fixture grids in fixtures/grids/ pin the generator's exact output and are shared with web/'s
// rendering tests. A change to the algorithm fails here until the fixtures are regenerated on purpose:
//   UPDATE_FIXTURES=1 dotnet test api.Tests --filter MapFixtureTests
// and committed together with the web/ tests that read them.
public sealed class MapFixtureTests
{
    // The endpoint's JSON options, indented and with a trailing newline so fixture diffs stay readable.
    private static readonly JsonSerializerOptions FixtureJson = CreateFixtureJson();

    public static TheoryData<uint> Seeds => new() { 1, 42, 20260925 };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Default_size_map_matches_its_fixture(uint seed)
    {
        var map = BspGenerator.Generate(seed, MapSize.DefaultWidth, MapSize.DefaultHeight);
        var actual = JsonSerializer.Serialize(map, FixtureJson) + "\n";
        var path = Path.Combine(FindRepoRoot(), "fixtures", "grids", $"seed-{seed}.json");

        if (Environment.GetEnvironmentVariable("UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"Missing fixture {path}. Run the tests with UPDATE_FIXTURES=1 to create it.");
        // Line endings are normalised so a checkout with CRLF conversion still compares equal.
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    private static JsonSerializerOptions CreateFixtureJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        MapJson.Configure(options);
        return options;
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
