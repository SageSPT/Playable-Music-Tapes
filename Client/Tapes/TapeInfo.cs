using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal class TapeInfo
{
    [JsonProperty("tpl")] public string Tpl;
    [JsonProperty("tapeId")] public string TapeId;
    [JsonProperty("title")] public string Title;
    [JsonProperty("bundle")] public string Bundle;
    [JsonProperty("filter")] public string Filter;
    [JsonProperty("tracks")] public List<TrackInfo> Tracks;
}
