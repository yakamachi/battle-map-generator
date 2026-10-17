using BattleMapGenerator.Api.Maps;

namespace BattleMapGenerator.Api.Data;

// One map in an account's library. The grid is stored, not only the seed: a seed reproduces its map
// only while the generator is unchanged, so the row keeps the GeneratedMap the account saved, compactly:
// the parameters as columns and the cells and rooms as bytes (GridCodec). The list reads the summary
// columns only and never touches Cells or Rooms.
public class SavedMap
{
    public const int MaxTitleLength = 80;

    // One byte per cell, at the 60×60 cap.
    public const int MaxCellBytes = MapSize.MaxWidth * MapSize.MaxHeight;

    // Five bytes per room, at the 12-room cap.
    public const int MaxRoomBytes = MapSize.MaxRoomCount * GridCodec.BytesPerRoom;

    // Long enough for every EncounterType and BossSize name.
    public const int MaxEnumNameLength = 16;

    public Guid Id { get; set; }

    // AspNetUsers.Id; deleting the account deletes its maps.
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // Holds the map's uint seed.
    public long Seed { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int RoomCount { get; set; }

    // Stored by its JSON name ("skirmish", "boss"), see AppDbContext.
    public EncounterType Encounter { get; set; }

    // Set only on a boss fight; stored by its JSON name ("large", "huge", "gargantuan").
    public BossSize? BossSize { get; set; }

    // GridCodec.EncodeCells: one CellKind number per cell, row-major.
    public byte[] Cells { get; set; } = [];

    // GridCodec.EncodeRooms: X, Y, Width, Height and the RoomKind number per room.
    public byte[] Rooms { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
