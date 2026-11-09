using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal static class TapePositions
{
    private static Dictionary<string, TapePosition> _positions;

    private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, Plugin.Guid + ".positions.json");

    public static TapePosition Get(string itemId)
    {
        LoadIfNeeded();
        if (itemId != null && _positions.TryGetValue(itemId, out TapePosition position))
        {
            return position;
        }

        return new TapePosition();
    }

    public static void Set(string itemId, int track, float time)
    {
        if (itemId == null)
        {
            return;
        }

        LoadIfNeeded();
        _positions[itemId] = new TapePosition { Track = track, Time = time };
        Save();
    }

    public static void Reset(string itemId)
    {
        LoadIfNeeded();
        if (itemId != null && _positions.Remove(itemId))
        {
            Save();
        }
    }

    private static void LoadIfNeeded()
    {
        if (_positions != null)
        {
            return;
        }

        _positions = new Dictionary<string, TapePosition>();
        if (!File.Exists(FilePath))
        {
            return;
        }

        try
        {
            var saved = JsonConvert.DeserializeObject<Dictionary<string, TapePosition>>(File.ReadAllText(FilePath));
            if (saved != null)
            {
                _positions = saved;
            }
        }
        catch (Exception error)
        {
            Log.Warning("Tapes", "tape positions unreadable, starting fresh: " + error.Message);
        }
    }

    private static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_positions, Formatting.Indented));
        }
        catch (Exception error)
        {
            Log.Warning("Tapes", "tape positions not saved: " + error.Message);
        }
    }
}
