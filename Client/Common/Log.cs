using System.Collections.Generic;
using BepInEx.Logging;

namespace PlayableMusicTapes.Client;

internal static class Log
{
    public static ManualLogSource Source;

    private static readonly HashSet<string> AlreadyWritten = new HashSet<string>();

    public static void Info(string category, string message)
    {
        Source?.LogInfo(Format(category, message));
    }

    public static void Warning(string category, string message)
    {
        string line = Format(category, message);
        if (Source != null && FirstTime(line))
        {
            Source.LogWarning(line);
        }
    }

    public static void Error(string category, string message)
    {
        string line = Format(category, message);
        if (Source != null && FirstTime(line))
        {
            Source.LogError(line);
        }
    }

    private static string Format(string category, string message)
    {
        return "[" + category + "] " + message;
    }

    private static bool FirstTime(string line)
    {
        lock (AlreadyWritten)
        {
            return AlreadyWritten.Add(line);
        }
    }
}
