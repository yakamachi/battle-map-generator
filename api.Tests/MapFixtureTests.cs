using System.Text.Json;
using BattleMapGenerator.Api.Maps;

namespace BattleMapGenerator.Api.Tests;

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
        AssertMatchesFixture(map, $"seed-{seed}.json");
    }

    // A room count other than the default, so the splitting to an exact count is pinned too.
    [Fact]
    public void Three_room_map_matches_its_fixture()
    {
        var map = BspGenerator.Generate(42, new MapParameters(3, EncounterType.Skirmish, null));
        AssertMatchesFixture(map, "seed-42-rooms-3.json");
    }

    // A boss fight, so the arena's placement and its cells are pinned and web/ draws an arena.
    [Fact]
    public void Boss_map_matches_its_fixture()
    {
        var map = BspGenerator.Generate(42, new MapParameters(6, EncounterType.Boss, BossSize.Huge));
        AssertMatchesFixture(map, "seed-42-boss-huge.json");
    }

    private static void AssertMatchesFixture(GeneratedMap map, string fileName)
    {
        var actual = JsonSerializer.Serialize(map, FixtureJson) + "\n";
        var path = Path.Combine(FindRepoRoot(), "fixtures", "grids", fileName);

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
