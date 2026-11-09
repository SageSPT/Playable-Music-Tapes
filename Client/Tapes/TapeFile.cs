using System.Collections.Generic;
using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal class TapeFile
{
    [JsonProperty("recorder")] public string Recorder;
    [JsonProperty("slot")] public string Slot;
    [JsonProperty("playback")] public TapePlayback Playback;
    [JsonProperty("tapes")] public List<TapeInfo> Tapes;
}
