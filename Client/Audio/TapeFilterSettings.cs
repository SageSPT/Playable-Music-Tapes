using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;

namespace PlayableMusicTapes.Client;

internal static class TapeFilterSettings
{
    private const string SoundSection = "Sound";
    private const string FilterSection = "Tape filters";
    private const string InvalidKeyCharacters = "[=\\n\\t\\\\\"'\\[\\]]";

    private static readonly Dictionary<string, ConfigEntry<TapeFilterPreset>> Presets = new Dictionary<string, ConfigEntry<TapeFilterPreset>>();
    private static ConfigEntry<bool> _spatial;
    private static ConfigEntry<float> _volume;
    private static ConfigEntry<float> _hissLevel;

    public static bool Spatial3D => _spatial?.Value ?? false;

    public static float Volume => _volume?.Value ?? 0.8f;

    public static float HissLevel => _hissLevel?.Value ?? 1f;

    public static void Bind(ConfigFile config, IEnumerable<TapeInfo> tapes)
    {
        _spatial = config.Bind(SoundSection, "3D sound", false,
            new ConfigDescription("Off = the tape plays straight in your ears like a Walkman. On = it plays from a speaker on your character (pans when you turn).",
                null, new ConfigurationManagerAttributes { Order = 0 }));
        _volume = config.Bind(SoundSection, "Volume", 0.8f,
            new ConfigDescription("Tape playback volume",
                new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 2 }));
        _hissLevel = config.Bind(SoundSection, "Hiss level", 1f,
            new ConfigDescription("Tape hiss / static amount for the filters that have it (0 = none, 2 = double)",
                new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes { Order = 1 }));

        int order = 1000;
        foreach (TapeInfo tape in tapes)
        {
            string key = Regex.Replace(tape.Title ?? tape.TapeId, InvalidKeyCharacters, "").Trim();
            TapeFilterPreset defaultPreset = Enum.TryParse(tape.Filter, true, out TapeFilterPreset parsed) ? parsed : TapeFilterPreset.WornCassette;
            string description = "Sound of the mixtape \"" + tape.Title + "\" (all its songs). Clean = original audio. Changes apply instantly, even mid-song.";

            Presets[tape.TapeId] = config.Bind(FilterSection, key, defaultPreset,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes { Order = order }));
            order--;
        }
    }

    public static TapeFilterPreset PresetFor(string tapeId)
    {
        if (tapeId != null && Presets.TryGetValue(tapeId, out ConfigEntry<TapeFilterPreset> entry))
        {
            return entry.Value;
        }

        return TapeFilterPreset.Clean;
    }

    public static void CyclePreset(string tapeId, int direction)
    {
        if (tapeId == null || !Presets.TryGetValue(tapeId, out ConfigEntry<TapeFilterPreset> entry))
        {
            return;
        }

        int presetCount = Enum.GetValues(typeof(TapeFilterPreset)).Length;
        int next = ((int)entry.Value + direction + presetCount) % presetCount;
        entry.Value = (TapeFilterPreset)next;
    }

    public static void ChangeVolume(int steps)
    {
        if (_volume == null)
        {
            return;
        }

        float tenths = (float)Math.Round(_volume.Value * 10f + steps);
        _volume.Value = Math.Max(0f, Math.Min(1f, tenths / 10f));
    }
}
