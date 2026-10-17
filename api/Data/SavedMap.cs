namespace BattleMapGenerator.Api.Data;

// One map in an account's library. The grid is stored, not only the seed: a seed reproduces its map
// only while the generator is unchanged, so MapJson keeps the full GeneratedMap the account saved.
// Seed, Width and Height repeat what MapJson holds, for the list.
public class SavedMap
{
    public const int MaxTitleLength = 80;

    public Guid Id { get; set; }

    // AspNetUsers.Id; deleting the account deletes its maps.
    public string OwnerId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    // Holds the map's uint seed.
    public long Seed { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    // The GeneratedMap serialised with the API's JSON options (MapJson).
    public string MapJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
