using System;
using BepInEx;
using BepInEx.Configuration;
using SPT.Reflection.Patching;
using UnityEngine;

namespace PlayableMusicTapes.Client;

[BepInPlugin(Guid, "Playable Music Tapes", "1.0.0")]
[BepInDependency("com.SPT.core", "4.1.6")]
[BepInDependency("com.sage.tutorialbackport", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.sage.playablemusictapes";

    private const string RecorderSection = "Recorder";

    public static ConfigEntry<KeyboardShortcut> TapeKey;
    public static ConfigEntry<KeyboardShortcut> PlayKey;
    public static ConfigEntry<KeyboardShortcut> NextKey;
    public static ConfigEntry<KeyboardShortcut> InsertKey;
    public static ConfigEntry<KeyboardShortcut> EjectKey;
    public static ConfigEntry<KeyboardShortcut> RestartKey;
    public static ConfigEntry<KeyboardShortcut> FilterNextKey;
    public static ConfigEntry<KeyboardShortcut> FilterPrevKey;
    public static ConfigEntry<KeyboardShortcut> VolumeUpKey;
    public static ConfigEntry<KeyboardShortcut> VolumeDownKey;

    private static int _failedPatches;

    private void Awake()
    {
        Log.Source = Logger;
        TapeKey = BindKey("Take out / put away", KeyCode.M, 5,
            "Takes the mixtape player out (held still in your hands, with the player HUD) / puts it away. In raid.");
        PlayKey = BindKey("Play / stop", KeyCode.Mouse0, 4,
            "While holding the recorder: plays / stops the loaded tape (resumes where you stopped)");
        NextKey = BindKey("Next song", KeyCode.K, 3,
            "While holding the recorder: skips to the next song");
        InsertKey = BindKey("Insert selected tape", KeyCode.R, 2,
            "While holding the recorder: puts the tape highlighted in the HUD (mouse wheel) in, swapping out the loaded one");
        EjectKey = BindKey("Eject tape", KeyCode.J, 1,
            "While holding the recorder: takes the loaded tape out into your inventory");
        RestartKey = BindKey("Restart tape", KeyCode.Backspace, 0,
            "While holding the recorder: plays the loaded tape from the start (song 1, 0:00)");
        FilterNextKey = BindKey("Next filter", KeyCode.RightArrow, -1,
            "While holding the recorder: next sound filter for the loaded tape (saved per tape)");
        FilterPrevKey = BindKey("Previous filter", KeyCode.LeftArrow, -2,
            "While holding the recorder: previous sound filter for the loaded tape");
        VolumeUpKey = BindKey("Volume up", KeyCode.UpArrow, -3,
            "While holding the recorder: volume +10 %");
        VolumeDownKey = BindKey("Volume down", KeyCode.DownArrow, -4,
            "While holding the recorder: volume -10 %");
        TapeFilterSettings.Bind(Config, TapeCatalog.AllTapes);

        Enable(new GameStartedPatch());
        Enable(new GameEndPatch());
        Enable(new NotificationReturnToPoolPatch());
        Enable(new InteractionStatePatch());
        Enable(new InitializeControllerPatch());
        Enable(new SpawnOperationPatch());
        Enable(new WeapOutPatch());
        Enable(new RecorderSoundPatch());
        Enable(new IkProcessPatch());
        Enable(new ItemContextInteractionsPatch());
        Enable(new SpecialIconPatch());
        Enable(new TapeSlotIconPatch());
        Enable(new MoveInputBlockPatch());
        Enable(new PlayerInputBlockPatch());

        Log.Info("Plugin", $"{Info.Metadata.Name} {Info.Metadata.Version} by Sage loaded" + (_failedPatches > 0 ? $" ({_failedPatches} patch(es) failed, see above)" : string.Empty));
    }

    public static string Label(ConfigEntry<KeyboardShortcut> entry)
    {
        KeyCode key = entry.Value.MainKey;
        return key switch
        {
            KeyCode.Mouse0 => "LMB",
            KeyCode.Mouse1 => "RMB",
            KeyCode.Mouse2 => "MMB",
            KeyCode.LeftArrow => "Left",
            KeyCode.RightArrow => "Right",
            KeyCode.UpArrow => "Up",
            KeyCode.DownArrow => "Down",
            _ => key.ToString()
        };
    }

    public static bool Pressed(ConfigEntry<KeyboardShortcut> entry)
    {
        KeyboardShortcut shortcut = entry.Value;
        if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey))
        {
            return false;
        }

        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (!Input.GetKey(modifier))
            {
                return false;
            }
        }

        return true;
    }

    private ConfigEntry<KeyboardShortcut> BindKey(string name, KeyCode key, int order, string description) =>
        Config.Bind(RecorderSection, name, new KeyboardShortcut(key),
            new ConfigDescription(description, null, new ConfigurationManagerAttributes { Order = order }));

    private static void Enable(ModulePatch patch)
    {
        try
        {
            patch.Enable();
        }
        catch (Exception ex)
        {
            _failedPatches++;
            Log.Error("Plugin", $"{patch.GetType().Name} could not be applied, that feature is off: {ex.Message}");
        }
    }
}
