using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal class TapePlayback
{
    [JsonProperty("minDistance")] public float MinDistance = 1.5f;
    [JsonProperty("maxDistance")] public float MaxDistance = 25f;
}
