using System;

namespace PlayableMusicTapes.Client;

internal struct Biquad
{
    private const float ButterworthQ = 0.7071f;

    private float _b0;
    private float _b1;
    private float _b2;
    private float _a1;
    private float _a2;

    private float _state1;
    private float _state2;

    public static Biquad LowPass(float frequency, float sampleRate)
    {
        double angle = Angle(frequency, sampleRate);
        double cos = Math.Cos(angle);
        double alpha = Alpha(angle, ButterworthQ);

        return Create(
            (1 - cos) / 2, 1 - cos, (1 - cos) / 2,
            1 + alpha, -2 * cos, 1 - alpha);
    }

    public static Biquad HighPass(float frequency, float sampleRate)
    {
        double angle = Angle(frequency, sampleRate);
        double cos = Math.Cos(angle);
        double alpha = Alpha(angle, ButterworthQ);

        return Create(
            (1 + cos) / 2, -(1 + cos), (1 + cos) / 2,
            1 + alpha, -2 * cos, 1 - alpha);
    }

    public static Biquad Peak(float frequency, float sampleRate, float gainDb, float q)
    {
        double angle = Angle(frequency, sampleRate);
        double cos = Math.Cos(angle);
        double alpha = Alpha(angle, q);
        double gain = Math.Pow(10, gainDb / 40.0);

        return Create(
            1 + alpha * gain, -2 * cos, 1 - alpha * gain,
            1 + alpha / gain, -2 * cos, 1 - alpha / gain);
    }

    public void CopyCoefficients(Biquad other)
    {
        _b0 = other._b0;
        _b1 = other._b1;
        _b2 = other._b2;
        _a1 = other._a1;
        _a2 = other._a2;
    }

    public float Process(float input)
    {
        float output = _b0 * input + _state1;
        _state1 = _b1 * input - _a1 * output + _state2;
        _state2 = _b2 * input - _a2 * output;

        if (float.IsNaN(output) || float.IsInfinity(output))
        {
            _state1 = 0f;
            _state2 = 0f;
            return 0f;
        }

        return output;
    }

    private static double Angle(float frequency, float sampleRate)
    {
        float safeFrequency = Math.Max(10f, Math.Min(frequency, sampleRate * 0.45f));
        return 2.0 * Math.PI * safeFrequency / sampleRate;
    }

    private static double Alpha(double angle, float q)
    {
        return Math.Sin(angle) / (2.0 * q);
    }

    private static Biquad Create(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        return new Biquad
        {
            _b0 = (float)(b0 / a0),
            _b1 = (float)(b1 / a0),
            _b2 = (float)(b2 / a0),
            _a1 = (float)(a1 / a0),
            _a2 = (float)(a2 / a0)
        };
    }
}
