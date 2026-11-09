using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using Newtonsoft.Json;

namespace PlayableMusicTapes.Client;

internal static class TapeCatalog
{
    private static TapeFile _data;

    public static string DataDir => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data");

    public static bool Loaded => Data != null;

    public static string RecorderTpl => Data?.Recorder;

    public static string SlotId => Data?.Slot ?? "mod_tape";

    public static TapePlayback Playback => Data?.Playback;

    public static IEnumerable<TapeInfo> AllTapes => Data?.Tapes ?? Enumerable.Empty<TapeInfo>();

    private static TapeFile Data
    {
        get
        {
            if (_data == null)
            {
                _data = Load();
            }

            return _data;
        }
    }

    public static bool IsTape(Item item)
    {
        return Find(item) != null;
    }

    public static string TitleOf(Item item)
    {
        return Find(item)?.Title;
    }

    public static string TapeIdOf(Item item)
    {
        return Find(item)?.TapeId;
    }

    public static TapeInfo Find(Item item)
    {
        string templateId = item?.StringTemplateId;
        if (templateId == null)
        {
            return null;
        }

        TapeFile data = Data;
        return data?.Tapes.FirstOrDefault(tape => tape.Tpl == templateId);
    }

    public static TapeInfo Playable(Item item)
    {
        TapeInfo tape = Find(item);
        if (tape?.Tracks == null || tape.Tracks.Count == 0)
        {
            return null;
        }

        return tape;
    }

    private static TapeFile Load()
    {
        string path = Path.Combine(DataDir, "tapes.json");
        try
        {
            var data = JsonConvert.DeserializeObject<TapeFile>(File.ReadAllText(path));
            Log.Info("Tapes", "loaded " + (data?.Tapes?.Count ?? 0) + " tapes from " + path);
            return data?.Tapes != null ? data : null;
        }
        catch (Exception error)
        {
            Log.Error("Tapes", "tape data unavailable (" + path + "): " + error.Message);
            return null;
        }
    }
}
