using System;
using System.Threading;
using SoundCalibrator.Audio.Interfaces;

namespace SoundCalibrator.Audio.Generators;

public enum TestSignalType
{
    PinkNoise,
    SineWave,
    SineSweep,
    GatedPinkNoise,
    IecNoise,
    PolarityPulse
}

/// <summary>
/// Generador de audio sintético para pruebas de calibración acústica y benchmarks en tiempo real.
/// Soporta Ruido Rosa (Kellet), Tono senoidal, Barrido senoidal (Farina), Ruido Rosa Racheado (Gated) y Ruido IEC 60268-1.
/// </summary>
public sealed class SyntheticAudioGenerator : IAudioCaptureDevice
{
    private readonly int _sampleRate;
    private readonly int _blockSize;
    private readonly Timer _timer;
    private readonly Random _random = new(1337);

    private readonly float[] _delayBuffer;
    private int _delayWriteIdx;
    private int _delaySamples;

    private float _gainFactor = 1.0f;
    private float _sinePhase;
    private float _sweepTime;
    private const float SweepDuration = 3.0f;
    private const float FStart = 20f;
    private const float FEnd = 20000f;

    // Gated Noise (Ráfagas)
    private float _gateTimeSeconds;
    public float GateOnMs { get; set; } = 500f;
    public float GateOffMs { get; set; } = 500f;

    // IEC 60268-1 filter states
    private float _iecLpState;
    private float _iecHpState;
    private float _iecPrevIn;

    private bool _isRunning;
    private bool _disposed;

    // Filtro Paul Kellet para generar Ruido Rosa (-3dB/octava)
    private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

    public string DeviceName => "Synthetic Loopback Acoustic Generator";
    public int SampleRate => _sampleRate;
    public int Channels => 2;
    public bool IsRunning => _isRunning;

    public TestSignalType SignalType { get; set; } = TestSignalType.PinkNoise;
    public float SineFrequency { get; set; } = 1000f;

    public float GainDb
    {
        get => 20f * MathF.Log10(_gainFactor);
        set => _gainFactor = MathF.Pow(10f, value / 20f);
    }

    public float DelayMs
    {
        get => (float)_delaySamples * 1000f / _sampleRate;
        set => _delaySamples = Math.Clamp((int)MathF.Round(value * _sampleRate / 1000f), 0, _delayBuffer.Length - 1);
    }

    public event EventHandler<AudioBlockEventArgs>? AudioBlockAvailable;

    public SyntheticAudioGenerator(int sampleRate = 48000, int blockSize = 512, int maxDelayMs = 200)
    {
        _sampleRate = sampleRate;
        _blockSize = blockSize;
        int maxDelaySamples = (int)MathF.Ceiling(maxDelayMs * sampleRate / 1000f) + blockSize * 2;
        _delayBuffer = new float[maxDelaySamples];

        int intervalMs = Math.Max(1, (int)MathF.Round((float)blockSize * 1000f / sampleRate));
        _timer = new Timer(OnTimerTick, null, Timeout.Infinite, intervalMs);
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        int intervalMs = Math.Max(1, (int)MathF.Round((float)_blockSize * 1000f / _sampleRate));
        _timer.Change(0, intervalMs);
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void OnTimerTick(object? state)
    {
        if (!_isRunning || _disposed) return;

        var refBlock = new float[_blockSize];
        var measBlock = new float[_blockSize];

        for (int i = 0; i < _blockSize; i++)
        {
            float sample = GenerateNextSample();
            refBlock[i] = sample;

            _delayBuffer[_delayWriteIdx] = sample;
            int readIdx = _delayWriteIdx - _delaySamples;
            if (readIdx < 0) readIdx += _delayBuffer.Length;

            measBlock[i] = _delayBuffer[readIdx] * _gainFactor;

            _delayWriteIdx = (_delayWriteIdx + 1) % _delayBuffer.Length;
        }

        AudioBlockAvailable?.Invoke(this, new AudioBlockEventArgs(refBlock, measBlock, _blockSize));
    }

    private float GenerateNextSample()
    {
        if (SignalType == TestSignalType.SineWave)
        {
            float val = MathF.Sin(_sinePhase);
            _sinePhase += 2f * MathF.PI * SineFrequency / _sampleRate;
            if (_sinePhase >= 2f * MathF.PI) _sinePhase -= 2f * MathF.PI;
            return val * 0.5f;
        }

        if (SignalType == TestSignalType.SineSweep)
        {
            float t = _sweepTime;
            float ratio = FEnd / FStart;
            float k = 2f * MathF.PI * FStart * SweepDuration / MathF.Log(ratio);
            float phase = k * (MathF.Pow(ratio, t / SweepDuration) - 1.0f);
            float val = MathF.Sin(phase) * 0.4f;

            _sweepTime += 1.0f / _sampleRate;
            if (_sweepTime >= SweepDuration) _sweepTime = 0f;

            return val;
        }

        // Ruido blanco base
        float white = (float)(_random.NextDouble() * 2.0 - 1.0) * 0.25f;

        // Ruido rosa con filtro Paul Kellet
        _b0 = 0.99886f * _b0 + white * 0.0555179f;
        _b1 = 0.99332f * _b1 + white * 0.0750759f;
        _b2 = 0.96900f * _b2 + white * 0.1538520f;
        _b3 = 0.86650f * _b3 + white * 0.3104856f;
        _b4 = 0.55000f * _b4 + white * 0.5329522f;
        _b5 = -0.7616f * _b5 - white * 0.0168980f;
        float pink = (_b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f) * 0.15f;
        _b6 = white * 0.115926f;

        if (SignalType == TestSignalType.PinkNoise)
        {
            return pink;
        }

        if (SignalType == TestSignalType.GatedPinkNoise)
        {
            float onSec = GateOnMs / 1000f;
            float offSec = GateOffMs / 1000f;
            float periodSec = onSec + offSec;

            float phase = _gateTimeSeconds % periodSec;
            _gateTimeSeconds += 1.0f / _sampleRate;

            float env = 0f;
            const float fadeSec = 0.015f; // 15 ms fade to avoid clicks

            if (phase < onSec)
            {
                if (phase < fadeSec)
                {
                    env = 0.5f * (1f - MathF.Cos(MathF.PI * phase / fadeSec));
                }
                else if (phase > onSec - fadeSec)
                {
                    env = 0.5f * (1f + MathF.Cos(MathF.PI * (phase - (onSec - fadeSec)) / fadeSec));
                }
                else
                {
                    env = 1.0f;
                }
            }

            return pink * env;
        }

        if (SignalType == TestSignalType.IecNoise)
        {
            // Filtro IEC 60268-1: Pasa-bajos 5kHz + Pasa-altos 40Hz
            float dt = 1.0f / _sampleRate;
            float rcLp = 1.0f / (2f * MathF.PI * 5000f);
            float alphaLp = dt / (rcLp + dt);
            _iecLpState += alphaLp * (pink - _iecLpState);

            float rcHp = 1.0f / (2f * MathF.PI * 40f);
            float alphaHp = rcHp / (rcHp + dt);
            _iecHpState = alphaHp * (_iecHpState + _iecLpState - _iecPrevIn);
            _iecPrevIn = _iecLpState;

            return _iecHpState * 1.3f;
        }

        if (SignalType == TestSignalType.PolarityPulse)
        {
            float periodSec = 0.5f; // Click cada 500 ms (2 Hz)
            float t = _gateTimeSeconds % periodSec;
            _gateTimeSeconds += 1.0f / _sampleRate;

            const float pulseWidthSec = 0.0015f; // Pulso positivo de 1.5 ms
            if (t < pulseWidthSec)
            {
                return 0.85f * MathF.Sin(MathF.PI * t / pulseWidthSec);
            }
            return 0f;
        }

        return white;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _timer.Dispose();
    }
}
