using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal class TapePosition
{
    [JsonProperty("track")] public int Track;
    [JsonProperty("time")] public float Time;
}
