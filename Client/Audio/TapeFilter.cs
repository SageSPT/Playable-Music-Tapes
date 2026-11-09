using System;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal sealed class TapeFilter : MonoBehaviour
{
    private const int MaxChannels = 8;
    private const float DelayBufferSeconds = 0.06f;
    private const float BaseDelaySeconds = 0.02f;
    private const float HissLowCut = 1500f;
    private const float HissHighCut = 9000f;
    private const float CrackleVolume = 0.35f;
    private const float DropoutsPerSecond = 0.25f;
    private const float DropoutMinSeconds = 0.06f;
    private const float DropoutExtraSeconds = 0.15f;
    private const float DropoutDepth = 2.6f;
    private const float DropoutSmoothing = 0.002f;

    private readonly Biquad[] _lowCut = new Biquad[MaxChannels];
    private readonly Biquad[] _bass = new Biquad[MaxChannels];
    private readonly Biquad[] _mid = new Biquad[MaxChannels];
    private readonly Biquad[] _highCut = new Biquad[MaxChannels];
    private Biquad _hissLowCut;
    private Biquad _hissHighCut;

    private float[][] _delayLines;
    private int _writeIndex;
    private int _sampleRate;
    private double _wowPhase;
    private double _flutterPhase;
    private uint _randomState = 0x9E3779B9u;

    private float _dropoutGain = 1f;
    private float _dropoutTarget = 1f;
    private int _dropoutSamplesLeft;

    private float _drive = 1f;
    private float _driveCompensation = 1f;

    private volatile FilterProfile _profile;
    private FilterProfile _activeProfile;
    private TapeFilterPreset _preset = (TapeFilterPreset)(-1);
    private float _hissLevel = -1f;
    private string _tapeId;

    public string TapeId
    {
        get => _tapeId;
        set
        {
            _tapeId = value;
            Refresh();
        }
    }

    private void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        int bufferLength = Mathf.CeilToInt(_sampleRate * DelayBufferSeconds);
        _delayLines = new float[MaxChannels][];
        for (int channel = 0; channel < MaxChannels; channel++)
        {
            _delayLines[channel] = new float[bufferLength];
        }

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        TapeFilterPreset preset = TapeFilterSettings.PresetFor(TapeId);
        float hissLevel = TapeFilterSettings.HissLevel;
        if (preset == _preset && Mathf.Approximately(hissLevel, _hissLevel))
        {
            return;
        }

        _preset = preset;
        _hissLevel = hissLevel;
        _profile = FilterProfile.For(preset, hissLevel);
        Log.Info("Filter", (TapeId ?? "?") + ": " + preset);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        FilterProfile profile = _profile;
        if (profile == null || _delayLines == null || channels <= 0)
        {
            return;
        }

        if (profile.Bypass)
        {
            KeepDelayLinesFilled(data, channels);
            return;
        }

        if (!ReferenceEquals(profile, _activeProfile))
        {
            _activeProfile = profile;
            SetUpFilters(profile);
        }

        int filteredChannels = Math.Min(channels, MaxChannels);
        int bufferLength = _delayLines[0].Length;
        double wowStep = 2.0 * Math.PI * profile.WowRate / _sampleRate;
        double flutterStep = 2.0 * Math.PI * profile.FlutterRate / _sampleRate;
        float wowAmount = profile.WowRate > 0f ? profile.WowDepth / (2f * Mathf.PI * profile.WowRate) : 0f;
        float flutterAmount = profile.FlutterRate > 0f ? profile.FlutterDepth / (2f * Mathf.PI * profile.FlutterRate) : 0f;
        int frames = data.Length / channels;

        for (int frame = 0; frame < frames; frame++)
        {
            int first = frame * channels;

            _wowPhase = NextPhase(_wowPhase, wowStep);
            _flutterPhase = NextPhase(_flutterPhase, flutterStep);
            float delaySamples = (BaseDelaySeconds + wowAmount * (float)Math.Sin(_wowPhase) + flutterAmount * (float)Math.Sin(_flutterPhase)) * _sampleRate;

            float readPosition = _writeIndex - delaySamples;
            if (readPosition < 0f)
            {
                readPosition += bufferLength;
            }

            if (readPosition >= bufferLength)
            {
                readPosition -= bufferLength;
            }

            int readIndex = (int)readPosition;
            int nextReadIndex = readIndex + 1 >= bufferLength ? 0 : readIndex + 1;
            float blend = readPosition - readIndex;

            float mono = profile.Mono ? Average(data, first, channels) : 0f;
            float noise = NextNoise(profile);
            if (profile.Dropouts > 0f)
            {
                UpdateDropout(profile);
            }

            for (int channel = 0; channel < filteredChannels; channel++)
            {
                float[] delayLine = _delayLines[channel];
                delayLine[_writeIndex] = profile.Mono ? mono : data[first + channel];

                float delayed = delayLine[readIndex] + (delayLine[nextReadIndex] - delayLine[readIndex]) * blend;
                float shaped = Saturate(Equalize(channel, delayed));
                data[first + channel] = (shaped * _dropoutGain + noise) * profile.OutputGain;
            }

            for (int channel = filteredChannels; channel < channels; channel++)
            {
                data[first + channel] = 0f;
            }

            AdvanceWriteIndex(bufferLength);
        }
    }

    private void SetUpFilters(FilterProfile profile)
    {
        Biquad lowCut = Biquad.HighPass(profile.LowCut, _sampleRate);
        Biquad highCut = Biquad.LowPass(profile.HighCut, _sampleRate);
        Biquad bass = Biquad.Peak(profile.BassFrequency, _sampleRate, profile.BassGain, 1f);
        Biquad mid = Biquad.Peak(profile.MidFrequency, _sampleRate, profile.MidGain, 1.2f);
        for (int channel = 0; channel < MaxChannels; channel++)
        {
            _lowCut[channel].CopyCoefficients(lowCut);
            _highCut[channel].CopyCoefficients(highCut);
            _bass[channel].CopyCoefficients(bass);
            _mid[channel].CopyCoefficients(mid);
        }

        _hissLowCut = Biquad.HighPass(HissLowCut, _sampleRate);
        _hissHighCut = Biquad.LowPass(HissHighCut, _sampleRate);
        _drive = Math.Max(0.01f, profile.Drive);
        _driveCompensation = 1f / (float)Math.Tanh(_drive);
    }

    private float Equalize(int channel, float sample)
    {
        sample = _lowCut[channel].Process(sample);
        sample = _bass[channel].Process(sample);
        sample = _mid[channel].Process(sample);
        sample = _highCut[channel].Process(sample);
        return sample;
    }

    private float Saturate(float sample)
    {
        return (float)Math.Tanh(sample * _drive) * _driveCompensation;
    }

    private float NextNoise(FilterProfile profile)
    {
        float noise = 0f;
        if (profile.Hiss > 0f)
        {
            float whiteNoise = NextRandom() * 2f - 1f;
            noise = _hissHighCut.Process(_hissLowCut.Process(whiteNoise)) * profile.Hiss * 3f;
        }

        if (profile.Crackle > 0f && NextRandom() < profile.Crackle)
        {
            noise += (NextRandom() * 2f - 1f) * CrackleVolume;
        }

        return noise;
    }

    private void UpdateDropout(FilterProfile profile)
    {
        if (_dropoutSamplesLeft > 0)
        {
            _dropoutSamplesLeft--;
            if (_dropoutSamplesLeft == 0)
            {
                _dropoutTarget = 1f;
            }
        }
        else if (NextRandom() < DropoutsPerSecond / _sampleRate)
        {
            _dropoutSamplesLeft = (int)(_sampleRate * (DropoutMinSeconds + NextRandom() * DropoutExtraSeconds));
            _dropoutTarget = 1f - profile.Dropouts * DropoutDepth;
        }

        _dropoutGain += (_dropoutTarget - _dropoutGain) * DropoutSmoothing;
    }

    private void KeepDelayLinesFilled(float[] data, int channels)
    {
        int filteredChannels = Math.Min(channels, MaxChannels);
        int bufferLength = _delayLines[0].Length;
        for (int first = 0; first + channels <= data.Length; first += channels)
        {
            for (int channel = 0; channel < filteredChannels; channel++)
            {
                _delayLines[channel][_writeIndex] = data[first + channel];
            }

            AdvanceWriteIndex(bufferLength);
        }
    }

    private void AdvanceWriteIndex(int bufferLength)
    {
        _writeIndex = _writeIndex + 1 >= bufferLength ? 0 : _writeIndex + 1;
    }

    private static double NextPhase(double phase, double step)
    {
        phase += step;
        if (phase > 2 * Math.PI)
        {
            phase -= 2 * Math.PI;
        }

        return phase;
    }

    private static float Average(float[] data, int first, int channels)
    {
        float sum = 0f;
        for (int channel = 0; channel < channels; channel++)
        {
            sum += data[first + channel];
        }

        return sum / channels;
    }

    private float NextRandom()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return (_randomState & 0xFFFFFF) / 16777216f;
    }
}
