using Android.Content.Res;

namespace Game.Elevator.Android;

internal static class AndroidAssetExtractor
{
    private const string AssetVersion = "1";

    public static string Extract(AssetManager assets, string filesDirectory)
    {
        var destination = Path.Combine(filesDirectory, "Assets");
        var versionFile = Path.Combine(destination, ".version");

        if (File.Exists(versionFile) &&
            File.ReadAllText(versionFile) == AssetVersion)
        {
            return destination;
        }

        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        Directory.CreateDirectory(destination);
        ExtractDirectory(assets, string.Empty, destination);
        File.WriteAllText(versionFile, AssetVersion);
        return destination;
    }

    private static void ExtractDirectory(
        AssetManager assets,
        string assetPath,
        string destination)
    {
        var entries = assets.List(assetPath) ?? [];
        if (entries.Length == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = assets.Open(assetPath, Access.Streaming);
            using var target = File.Create(destination);
            source.CopyTo(target);
            return;
        }

        Directory.CreateDirectory(destination);
        foreach (var entry in entries)
        {
            var childAssetPath = string.IsNullOrEmpty(assetPath)
                ? entry
                : $"{assetPath}/{entry}";
            ExtractDirectory(
                assets,
                childAssetPath,
                Path.Combine(destination, entry));
        }
    }
}
