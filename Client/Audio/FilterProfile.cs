namespace PlayableMusicTapes.Client;

internal sealed class FilterProfile
{
    public bool Bypass;
    public bool Mono;

    public float LowCut = 20f;
    public float HighCut = 20000f;
    public float BassFrequency = 170f;
    public float BassGain;
    public float MidFrequency = 2300f;
    public float MidGain;

    public float WowDepth;
    public float WowRate = 0.6f;
    public float FlutterDepth;
    public float FlutterRate = 6f;

    public float Drive = 1f;
    public float Hiss;
    public float Crackle;
    public float Dropouts;
    public float OutputGain = 1f;

    public static FilterProfile For(TapeFilterPreset preset, float hissLevel)
    {
        FilterProfile profile = Create(preset);
        profile.Hiss *= hissLevel;
        return profile;
    }

    private static FilterProfile Create(TapeFilterPreset preset)
    {
        switch (preset)
        {
            case TapeFilterPreset.WornCassette:
                return new FilterProfile
                {
                    Mono = true,
                    LowCut = 90f,
                    HighCut = 8500f,
                    BassGain = -2f,
                    MidGain = 3f,
                    WowDepth = 0.0038f,
                    WowRate = 0.6f,
                    FlutterDepth = 0.0019f,
                    FlutterRate = 6f,
                    Drive = 1.3f,
                    Hiss = 0.008f,
                    OutputGain = 0.79f
                };

            case TapeFilterPreset.Walkman:
                return new FilterProfile
                {
                    LowCut = 50f,
                    HighCut = 12000f,
                    MidGain = 1.5f,
                    WowDepth = 0.0015f,
                    WowRate = 0.5f,
                    FlutterDepth = 0.0008f,
                    FlutterRate = 7f,
                    Drive = 1.2f,
                    Hiss = 0.004f,
                    OutputGain = 0.72f
                };

            case TapeFilterPreset.AmRadio:
                return new FilterProfile
                {
                    Mono = true,
                    LowCut = 300f,
                    HighCut = 3500f,
                    MidFrequency = 1500f,
                    MidGain = 4f,
                    Drive = 1.9f,
                    Hiss = 0.02f,
                    Crackle = 0.00002f,
                    OutputGain = 0.7f
                };

            case TapeFilterPreset.SmallSpeaker:
                return new FilterProfile
                {
                    Mono = true,
                    LowCut = 250f,
                    HighCut = 6000f,
                    MidFrequency = 3000f,
                    MidGain = 5f,
                    Drive = 1.5f,
                    OutputGain = 0.75f
                };

            case TapeFilterPreset.ChewedTape:
                return new FilterProfile
                {
                    Mono = true,
                    LowCut = 120f,
                    HighCut = 6000f,
                    BassGain = -3f,
                    MidGain = 2f,
                    WowDepth = 0.012f,
                    WowRate = 0.8f,
                    FlutterDepth = 0.005f,
                    FlutterRate = 9f,
                    Drive = 1.6f,
                    Hiss = 0.012f,
                    Dropouts = 0.25f,
                    OutputGain = 0.75f
                };

            default:
                return new FilterProfile { Bypass = true };
        }
    }
}
