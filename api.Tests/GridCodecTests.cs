using System.Text.Json;
using BattleMapGenerator.Api.Maps;
using BattleMapGenerator.Api.Tests.Infrastructure;

namespace BattleMapGenerator.Api.Tests;

// The byte layout of saved grids (GridCodec). Stored rows outlive code changes, so the enum numbers the
// bytes hold are pinned here: renumbering CellKind or RoomKind must turn this red.
public sealed class GridCodecTests
{
    public static TheoryData<string> FixtureFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(RepoRoot.Find(), "fixtures", "grids"), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public void Every_fixture_grid_round_trips(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot.Find(), "fixtures", "grids", fileName));
        var map = JsonSerializer.Deserialize<GeneratedMap>(json, MapDeterminismTests.Json())!;

        var cells = GridCodec.EncodeCells(map.Cells);
        var rooms = GridCodec.EncodeRooms(map.Rooms);

        Assert.Equal(map.Width * map.Height, cells.Length);
        Assert.Equal(map.Rooms.Length * GridCodec.BytesPerRoom, rooms.Length);
        Assert.Equal(map.Cells, GridCodec.DecodeCells(cells, map.Width, map.Height));
        Assert.Equal(map.Rooms, GridCodec.DecodeRooms(rooms));
    }

    // The exact bytes, so a change to the layout (or to an enum number) cannot pass as a round trip.
    [Fact]
    public void Cells_and_rooms_encode_to_the_documented_bytes()
    {
        CellKind[] cells = [CellKind.Void, CellKind.Floor, CellKind.Corridor, CellKind.Wall, CellKind.Door, CellKind.BossArena];
        Room[] rooms = [new(1, 2, 3, 4, RoomKind.Room), new(10, 20, 12, 12, RoomKind.BossArena)];

        Assert.Equal([0, 1, 2, 3, 4, 5], GridCodec.EncodeCells(cells));
        Assert.Equal([1, 2, 3, 4, 0, 10, 20, 12, 12, 1], GridCodec.EncodeRooms(rooms));
    }

    // Persisted in SavedMaps: append new values, never renumber.
    [Fact]
    public void Persisted_enum_numbers_are_pinned()
    {
        Assert.Equal(0, (int)CellKind.Void);
        Assert.Equal(1, (int)CellKind.Floor);
        Assert.Equal(2, (int)CellKind.Corridor);
        Assert.Equal(3, (int)CellKind.Wall);
        Assert.Equal(4, (int)CellKind.Door);
        Assert.Equal(5, (int)CellKind.BossArena);
        Assert.Equal(6, Enum.GetValues<CellKind>().Length);

        Assert.Equal(0, (int)RoomKind.Room);
        Assert.Equal(1, (int)RoomKind.BossArena);
        Assert.Equal(2, Enum.GetValues<RoomKind>().Length);
    }

    [Theory]
    [InlineData(5, 2, 3)]
    [InlineData(7, 2, 3)]
    [InlineData(0, 2, 3)]
    public void Cells_of_the_wrong_length_are_rejected(int length, int width, int height)
    {
        Assert.Throws<InvalidDataException>(() => GridCodec.DecodeCells(new byte[length], width, height));
    }

    [Fact]
    public void An_undefined_cell_kind_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => GridCodec.DecodeCells([0, 1, 6, 0], 2, 2));
        Assert.Throws<InvalidDataException>(() => GridCodec.DecodeCells([0, 1, 255, 0], 2, 2));
        Assert.Throws<InvalidDataException>(() => GridCodec.EncodeCells([CellKind.Floor, (CellKind)6]));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(9)]
    public void Room_bytes_that_are_not_a_multiple_of_five_are_rejected(int length)
    {
        Assert.Throws<InvalidDataException>(() => GridCodec.DecodeRooms(new byte[length]));
    }

    [Fact]
    public void An_undefined_room_kind_or_an_out_of_range_room_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => GridCodec.DecodeRooms([1, 2, 3, 4, 2]));
        Assert.Throws<InvalidDataException>(() => GridCodec.DecodeRooms([1, 2, 61, 4, 0]));
        Assert.Throws<InvalidDataException>(() => GridCodec.EncodeRooms([new Room(1, 2, 3, 4, (RoomKind)2)]));
        Assert.Throws<InvalidDataException>(() => GridCodec.EncodeRooms([new Room(-1, 2, 3, 4, RoomKind.Room)]));
        Assert.Throws<InvalidDataException>(() => GridCodec.EncodeRooms([new Room(1, 2, 61, 4, RoomKind.Room)]));
    }

    [Fact]
    public void An_empty_room_list_round_trips()
    {
        Assert.Empty(GridCodec.EncodeRooms([]));
        Assert.Empty(GridCodec.DecodeRooms([]));
    }
}
