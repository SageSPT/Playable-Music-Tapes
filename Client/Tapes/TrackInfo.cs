using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal class TrackInfo
{
    [JsonProperty("asset")] public string Asset;
    [JsonProperty("artist")] public string Artist;
    [JsonProperty("title")] public string Title;
    [JsonProperty("length")] public float Length;

    public string Label => Artist + " - " + Title;
}
