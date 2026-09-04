using System;
using System.Collections.Generic;

namespace SoundCalibrator.Core.Models;

public enum TargetCurvePreset
{
    None,
    Flat,
    HarmanTarget,
    BruelKjaer1974,
    CinemaXCurve,
    Custom
}

public readonly record struct TargetCurvePoint(float FrequencyHz, float GainDb);

public sealed class TargetCurve
{
    private readonly TargetCurvePoint[] _points;

    public string Name { get; }
    public IReadOnlyList<TargetCurvePoint> Points => _points;

    public TargetCurve(string name, IEnumerable<TargetCurvePoint> points)
    {
        Name = name;
        var list = new List<TargetCurvePoint>(points);
        list.Sort((a, b) => a.FrequencyHz.CompareTo(b.FrequencyHz));
        _points = [.. list];
    }

    public static TargetCurve CreatePreset(TargetCurvePreset preset)
    {
        return preset switch
        {
            TargetCurvePreset.Flat => new TargetCurve("Flat (0 dB)", [
                new TargetCurvePoint(20f, 0f),
                new TargetCurvePoint(20000f, 0f)
            ]),

            TargetCurvePreset.HarmanTarget => new TargetCurve("Harman Target", [
                new TargetCurvePoint(20f, 5.0f),
                new TargetCurvePoint(40f, 5.0f),
                new TargetCurvePoint(105f, 4.0f),
                new TargetCurvePoint(200f, 1.5f),
                new TargetCurvePoint(1000f, 0.0f),
                new TargetCurvePoint(10000f, -1.0f),
                new TargetCurvePoint(20000f, -2.0f)
            ]),

            TargetCurvePreset.BruelKjaer1974 => new TargetCurve("B&K 1974", [
                new TargetCurvePoint(20f, 3.0f),
                new TargetCurvePoint(50f, 3.0f),
                new TargetCurvePoint(1000f, 0.0f),
                new TargetCurvePoint(2000f, 0.0f),
                new TargetCurvePoint(20000f, -3.0f)
            ]),

            TargetCurvePreset.CinemaXCurve => new TargetCurve("Cinema X-Curve (ISO 2969)", [
                new TargetCurvePoint(20f, -1.0f),
                new TargetCurvePoint(40f, 0.0f),
                new TargetCurvePoint(2000f, 0.0f),
                new TargetCurvePoint(4000f, -3.0f),
                new TargetCurvePoint(8000f, -6.0f),
                new TargetCurvePoint(16000f, -9.0f),
                new TargetCurvePoint(20000f, -10.0f)
            ]),

            _ => new TargetCurve("None", [])
        };
    }

    public float Evaluate(float frequencyHz)
    {
        if (_points.Length == 0) return 0f;
        if (frequencyHz <= _points[0].FrequencyHz) return _points[0].GainDb;
        if (frequencyHz >= _points[^1].FrequencyHz) return _points[^1].GainDb;

        for (int i = 0; i < _points.Length - 1; i++)
        {
            ref readonly var p0 = ref _points[i];
            ref readonly var p1 = ref _points[i + 1];

            if (frequencyHz >= p0.FrequencyHz && frequencyHz <= p1.FrequencyHz)
            {
                if (p0.FrequencyHz <= 0f || p1.FrequencyHz <= 0f || frequencyHz <= 0f)
                {
                    float linearT = (frequencyHz - p0.FrequencyHz) / (p1.FrequencyHz - p0.FrequencyHz);
                    return p0.GainDb + linearT * (p1.GainDb - p0.GainDb);
                }

                float logF0 = MathF.Log10(p0.FrequencyHz);
                float logF1 = MathF.Log10(p1.FrequencyHz);
                float logF = MathF.Log10(frequencyHz);

                float t = (logF - logF0) / (logF1 - logF0);
                return p0.GainDb + t * (p1.GainDb - p0.GainDb);
            }
        }

        return 0f;
    }

    public void Evaluate(ReadOnlySpan<float> frequencies, Span<float> outGainDb)
    {
        int count = Math.Min(frequencies.Length, outGainDb.Length);
        for (int i = 0; i < count; i++)
        {
            outGainDb[i] = Evaluate(frequencies[i]);
        }
    }
}
