namespace BattleMapGenerator.Api.Tests.Infrastructure;

public static class RepoRoot
{
    public static string Find()
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
