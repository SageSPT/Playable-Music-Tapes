using Path = System.IO.Path;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SPTarkov.Server.Core.Models.Common;

namespace PlayableMusicTapes.Server;

public static class TapeIds
{
    public const string ModGuid = "com.sage.playablemusictapes";
    public const string RecorderTpl = "6a0e5a7c0b1d4e2f3a5c7000";
    public const string TapeSlot = "mod_tape";
    public const string Roubles = "5449016a4bdc2d6f028b456f";
    public const string GiftId = "PlayableTapesStarter3";
    public const string GiftLocaleKey = "playabletapes_starter_gift";

    private const string IdSeedPrefix = "playabletapes/";

    public static string ModDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    public static string File(params string[] parts)
    {
        return Path.Combine(ModDir, Path.Combine(parts));
    }

    public static MongoId Derived(string seed)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(IdSeedPrefix + seed));
        return new MongoId(Convert.ToHexString(hash, 0, 12).ToLowerInvariant());
    }
}
