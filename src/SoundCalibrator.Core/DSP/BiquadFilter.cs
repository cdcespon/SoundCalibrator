using System;
using System.Collections.Generic;
using SoundCalibrator.Core.Analysis;

namespace SoundCalibrator.Core.DSP;

/// <summary>
/// Implementación de filtro digital IIR Biquad de segundo orden (Direct Form I/II)
/// basado en el Audio EQ Cookbook de Robert Bristow-Johnson.
/// </summary>
public sealed class BiquadFilter
{
    private readonly float _b0, _b1, _b2;
    private readonly float _a1, _a2;
    private readonly float _sampleRate;

    public BiquadFilter(float b0, float b1, float b2, float a0, float a1, float a2, float sampleRate)
    {
        _sampleRate = sampleRate;
        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    public static BiquadFilter CreatePeq(float f0, float gainDb, float q, float sampleRate)
    {
        float w0 = 2f * MathF.PI * f0 / sampleRate;
        float alpha = MathF.Sin(w0) / (2f * Math.Max(0.1f, q));
        float a = MathF.Pow(10f, gainDb / 40f);

        float b0 = 1f + alpha * a;
        float b1 = -2f * MathF.Cos(w0);
        float b2 = 1f - alpha * a;
        float a0 = 1f + alpha / a;
        float a1 = -2f * MathF.Cos(w0);
        float a2 = 1f - alpha / a;

        return new BiquadFilter(b0, b1, b2, a0, a1, a2, sampleRate);
    }

    public float EvaluateDb(float freqHz)
    {
        float w = 2f * MathF.PI * freqHz / _sampleRate;
        float cos1 = MathF.Cos(w);
        float sin1 = MathF.Sin(w);
        float cos2 = MathF.Cos(2f * w);
        float sin2 = MathF.Sin(2f * w);

        float nRe = _b0 + _b1 * cos1 + _b2 * cos2;
        float nIm = -(_b1 * sin1 + _b2 * sin2);
        float nPwr = nRe * nRe + nIm * nIm;

        float dRe = 1f + _a1 * cos1 + _a2 * cos2;
        float dIm = -(_a1 * sin1 + _a2 * sin2);
        float dPwr = dRe * dRe + dIm * dIm;

        if (dPwr < 1e-12f) return 0f;
        return 10f * MathF.Log10(MathF.Max(1e-12f, nPwr / dPwr));
    }

    public void Evaluate(ReadOnlySpan<float> frequencies, Span<float> outGainDb)
    {
        int count = Math.Min(frequencies.Length, outGainDb.Length);
        for (int i = 0; i < count; i++)
        {
            outGainDb[i] = EvaluateDb(frequencies[i]);
        }
    }

    public static void EvaluateCascade(
        IReadOnlyList<PeqFilterSuggestion> filters,
        ReadOnlySpan<float> frequencies,
        Span<float> outTotalGainDb,
        float sampleRate)
    {
        int count = Math.Min(frequencies.Length, outTotalGainDb.Length);
        outTotalGainDb.Slice(0, count).Clear();

        if (filters == null || filters.Count == 0) return;

        foreach (var f in filters)
        {
            var biquad = CreatePeq(f.FrequencyHz, f.GainDb, f.Q, sampleRate);
            for (int i = 0; i < count; i++)
            {
                outTotalGainDb[i] += biquad.EvaluateDb(frequencies[i]);
            }
        }
    }
}
