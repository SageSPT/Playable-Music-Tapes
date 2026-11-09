using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace PlayableMusicTapes.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = TapeIds.ModGuid;
    public string Name { get; init; } = "Playable Music Tapes";
    public string Author { get; init; } = "Sage";
    public List<string>? Contributors { get; init; } = null;
    public Version Version { get; init; } = new Version(typeof(ModMetadata).Assembly.GetName().Version?.ToString(3), false);
    public Range SptVersion { get; init; } = new Range("~4.1.6", false);
    public List<string>? Incompatibilities { get; init; } = null;
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public bool HasPrepatcher { get; init; } = false;
    public string License { get; init; } = "CC BY-NC-ND 4.0";
}
