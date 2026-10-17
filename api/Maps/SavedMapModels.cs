using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BattleMapGenerator.Api.Maps;

// One row of the library list: enough to show and pick a map, without its grid.
public sealed record SavedMapSummary(
    Guid Id, string Title, uint Seed, int Width, int Height, MapParameters Parameters,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// A saved map with its full grid, rendered by web/ exactly like a freshly generated one.
public sealed record SavedMap(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, GeneratedMap Map);

// Save takes the generate fields plus a title. The seed is required: the server regenerates the map
// from seed and parameters, so the stored grid is always the generator's own output. The parameter
// rules are GenerateMapRequest's, run on ToGenerateRequest() (see SavedMapEndpoints).
public sealed record SaveMapRequest(
    [property: SavedMapTitle] string? Title,
    [property: Required(ErrorMessage = SaveMapRequest.SeedRequiredMessage)] uint? Seed,
    int? RoomCount = null,
    EncounterType? Encounter = null,
    BossSize? BossSize = null)
{
    public const string SeedRequiredMessage = "A saved map needs the seed it was generated with.";

    public GenerateMapRequest ToGenerateRequest() => new(Seed, RoomCount, Encounter, BossSize);
}

// Rename changes the title only.
public sealed record RenameMapRequest([property: SavedMapTitle] string? Title);

// Shared by Save and Rename, keyed "title". Leading and trailing whitespace is dropped before the
// length is checked; duplicate titles are allowed. A control character left after trimming (a newline,
// a tab, a bell), an invisible format character (zero-width space, a bidi override that reorders the
// text) or a line/paragraph separator is refused under the same message, so a list never shows a
// broken, multi-line or visually spoofed title.
[AttributeUsage(AttributeTargets.Property)]
public sealed class SavedMapTitleAttribute : ValidationAttribute
{
    public const string Message = "Title must be 1 to 80 characters.";

    public SavedMapTitleAttribute() : base(Message)
    {
    }

    public override bool IsValid(object? value) =>
        value is string title
        && Normalize(title) is { Length: >= 1 and <= Data.SavedMap.MaxTitleLength } normalized
        && !normalized.Any(IsRefused);

    public static string Normalize(string title) => title.Trim();

    private static bool IsRefused(char c) =>
        char.IsControl(c)
        || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
