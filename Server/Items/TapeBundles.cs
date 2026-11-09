using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Spt.Bundles;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;

namespace PlayableMusicTapes.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 3)]
public class TapeBundles(ISptLogger<TapeBundles> logger, BundleLoader bundleLoader, BundleHashCacheService hashes, JsonUtil jsonUtil) : IOnLoad
{
    public const string ManifestFile = "tapes-bundles.json";

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        string manifestPath = TapeIds.File(ManifestFile);
        if (!File.Exists(manifestPath))
        {
            logger.Error($"[Playable Music Tapes] {ManifestFile} missing, tapes and recorder will have no models");
            return;
        }

        var manifest = jsonUtil.Deserialize<TapeBundleManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken));
        string modPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), TapeIds.ModDir).Replace('\\', '/');
        HashSet<string> replace = manifest?.Replace?.ToHashSet() ?? [];
        int added = 0;
        int replaced = 0;
        int servedByOthers = 0;
        int missing = 0;

        foreach (BundleManifestEntry entry in manifest?.Manifest ?? [])
        {
            bool taken = bundleLoader.GetBundle(entry.Key) != null;
            if (taken && !replace.Contains(entry.Key))
            {
                servedByOthers++;
                continue;
            }

            BundleHashCacheEntry? hash = await HashBundleFile(modPath, entry.Key, cancellationToken);
            if (hash == null)
            {
                missing++;
                logger.Warning($"[Playable Music Tapes] bundle file missing: {entry.Key}");
                continue;
            }

            var info = new BundleInfo
            {
                ModPath = modPath,
                Bundle = entry,
                Crc = hash.Crc,
                Size = hash.Size,
                ModifiedUtcTicks = hash.ModifiedUtcTicks
            };

            if (!taken)
            {
                bundleLoader.AddBundle(entry.Key, info);
                added++;
            }
            else if (Replace(entry.Key, info))
            {
                replaced++;
            }
            else
            {
                logger.Warning($"[Playable Music Tapes] could not replace {entry.Key}, the recorder keeps its default look");
            }
        }

        logger.Info($"[Playable Music Tapes] bundles: {added} added, {replaced} replaced (recorder skin), {servedByOthers} already served by another mod, {missing} missing");
    }

    private bool Replace(string key, BundleInfo info)
    {
        FieldInfo? field = typeof(BundleLoader).GetField("_bundles", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(bundleLoader) is not ConcurrentDictionary<string, BundleInfo> bundles)
        {
            return false;
        }

        bundles[key] = info;
        return true;
    }

    private async Task<BundleHashCacheEntry?> HashBundleFile(string modPath, string bundleKey, CancellationToken cancellationToken)
    {
        string file = Path.Join(modPath, "bundles", bundleKey).Replace('\\', '/');
        if (!File.Exists(file))
        {
            return null;
        }

        return await hashes.GetOrCalculateHashAsync(file, cancellationToken);
    }

    private sealed class TapeBundleManifest
    {
        [JsonPropertyName("manifest")] public List<BundleManifestEntry> Manifest { get; set; } = [];
        [JsonPropertyName("replace")] public List<string> Replace { get; set; } = [];
    }
}
