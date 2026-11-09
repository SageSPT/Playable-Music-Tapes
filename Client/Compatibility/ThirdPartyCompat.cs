using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace PlayableMusicTapes.Client;

internal static class ThirdPartyCompat
{
    private const string TutorialBackportGuid = "com.sage.tutorialbackport";
    private const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static bool _looked;
    private static PropertyInfo _theirTapePlaying;
    private static PropertyInfo _theirRecorderBusy;
    private static MethodInfo _theirStop;

    public static bool TheirTapeActive()
    {
        FindTutorialBackport();
        try
        {
            return ReadFlag(_theirTapePlaying) || ReadFlag(_theirRecorderBusy);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void StopTheirTape()
    {
        FindTutorialBackport();
        try
        {
            _theirStop?.Invoke(null, null);
        }
        catch (Exception error)
        {
            Log.Warning("Compatibility", "could not stop the Tutorial Backport tape: " + error.Message);
        }
    }

    private static bool ReadFlag(PropertyInfo property)
    {
        return property != null && (bool)property.GetValue(null);
    }

    private static void FindTutorialBackport()
    {
        if (_looked)
        {
            return;
        }

        _looked = true;
        if (!Chainloader.PluginInfos.TryGetValue(TutorialBackportGuid, out var plugin) || plugin.Instance == null)
        {
            return;
        }

        Assembly assembly = plugin.Instance.GetType().Assembly;
        Type recorder = assembly.GetType("TutorialBackport.Client.TapeRecorder");
        Type listen = assembly.GetType("TutorialBackport.Client.TapeListen");
        _theirTapePlaying = recorder?.GetProperty("IsPlaying", StaticMembers);
        _theirStop = recorder?.GetMethod("StopPlaying", StaticMembers);
        _theirRecorderBusy = listen?.GetProperty("Busy", StaticMembers);

        Log.Info("Compatibility", "Tutorial Backport found: tape state " + (_theirTapePlaying != null) + ", recorder state " + (_theirRecorderBusy != null));
    }
}
