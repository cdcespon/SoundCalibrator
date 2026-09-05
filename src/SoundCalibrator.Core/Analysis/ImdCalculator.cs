using System;
using System.Collections.Generic;

namespace SoundCalibrator.Core.Analysis;

public enum ImdStandard
{
    Smpte,
    Ccif
}

public readonly record struct ImdProduct(
    string Name,
    float FrequencyHz,
    float LevelDb);

public sealed class ImdResult
{
    public required ImdStandard Standard { get; init; }
    public required float ImdPercent { get; init; }
    public required float ImdDb { get; init; }
    public required float PrimaryToneDb { get; init; }
    public required IReadOnlyList<ImdProduct> Products { get; init; }
}

/// <summary>
/// Analizador de Distorsión por Intermodulación (IMD) según los estándares SMPTE RP120 y CCIF (ITU-R DFD),
/// identificando productos de suma y diferencia generados por no-linealidades electroacústicas.
/// </summary>
public static class ImdCalculator
{
    public static ImdResult CalculateSmpte(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> spectrumDb,
        float f1 = 60.0f,
        float f2 = 7000.0f,
        float toleranceHz = 40.0f)
    {
        if (frequencies.IsEmpty || spectrumDb.IsEmpty || frequencies.Length != spectrumDb.Length)
        {
            return EmptyResult(ImdStandard.Smpte);
        }

        float carrierDb = FindPeak(frequencies, spectrumDb, f2, toleranceHz);
        if (carrierDb < -100.0f)
        {
            return EmptyResult(ImdStandard.Smpte);
        }

        float carrierVolt = MathF.Pow(10.0f, carrierDb / 20.0f);

        // SMPTE sidebands: f2 +/- f1, f2 +/- 2*f1
        float[] sidebandFreqs = [f2 - f1, f2 + f1, f2 - 2 * f1, f2 + 2 * f1];
        string[] names = ["f2 - f1", "f2 + f1", "f2 - 2f1", "f2 + 2f1"];

        var products = new List<ImdProduct>();
        float sumSquares = 0.0f;

        for (int i = 0; i < sidebandFreqs.Length; i++)
        {
            float f = sidebandFreqs[i];
            float level = FindPeak(frequencies, spectrumDb, f, toleranceHz);
            products.Add(new ImdProduct(names[i], f, level));
            float v = MathF.Pow(10.0f, level / 20.0f);
            sumSquares += v * v;
        }

        float imdRatio = MathF.Sqrt(sumSquares) / Math.Max(1e-12f, carrierVolt);
        float imdPercent = imdRatio * 100.0f;
        float imdDb = imdRatio > 1e-6f ? 20.0f * MathF.Log10(imdRatio) : -120.0f;

        return new ImdResult
        {
            Standard = ImdStandard.Smpte,
            ImdPercent = imdPercent,
            ImdDb = imdDb,
            PrimaryToneDb = carrierDb,
            Products = products
        };
    }

    public static ImdResult CalculateCcif(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> spectrumDb,
        float f1 = 19000.0f,
        float f2 = 20000.0f,
        float toleranceHz = 50.0f)
    {
        if (frequencies.IsEmpty || spectrumDb.IsEmpty || frequencies.Length != spectrumDb.Length)
        {
            return EmptyResult(ImdStandard.Ccif);
        }

        float p1Db = FindPeak(frequencies, spectrumDb, f1, toleranceHz);
        float p2Db = FindPeak(frequencies, spectrumDb, f2, toleranceHz);

        if (p1Db < -100.0f || p2Db < -100.0f)
        {
            return EmptyResult(ImdStandard.Ccif);
        }

        float v1 = MathF.Pow(10.0f, p1Db / 20.0f);
        float v2 = MathF.Pow(10.0f, p2Db / 20.0f);
        float primaryVolt = MathF.Sqrt(v1 * v1 + v2 * v2);

        // CCIF products: d2 = f2 - f1 (1kHz), d3a = 2*f1 - f2 (18kHz), d3b = 2*f2 - f1 (21kHz)
        float d2Freq = f2 - f1;
        float d3aFreq = 2 * f1 - f2;
        float d3bFreq = 2 * f2 - f1;

        float d2Db = FindPeak(frequencies, spectrumDb, d2Freq, toleranceHz);
        float d3aDb = FindPeak(frequencies, spectrumDb, d3aFreq, toleranceHz);
        float d3bDb = FindPeak(frequencies, spectrumDb, d3bFreq, toleranceHz);

        var products = new List<ImdProduct>
        {
            new("d2 (f2 - f1)", d2Freq, d2Db),
            new("d3a (2f1 - f2)", d3aFreq, d3aDb),
            new("d3b (2f2 - f1)", d3bFreq, d3bDb)
        };

        float sumSquares = 0.0f;
        foreach (var p in products)
        {
            float v = MathF.Pow(10.0f, p.LevelDb / 20.0f);
            sumSquares += v * v;
        }

        float imdRatio = MathF.Sqrt(sumSquares) / Math.Max(1e-12f, primaryVolt);
        float imdPercent = imdRatio * 100.0f;
        float imdDb = imdRatio > 1e-6f ? 20.0f * MathF.Log10(imdRatio) : -120.0f;

        return new ImdResult
        {
            Standard = ImdStandard.Ccif,
            ImdPercent = imdPercent,
            ImdDb = imdDb,
            PrimaryToneDb = Math.Max(p1Db, p2Db),
            Products = products
        };
    }

    private static float FindPeak(ReadOnlySpan<float> freqs, ReadOnlySpan<float> dbs, float targetFreq, float toleranceHz)
    {
        float maxDb = -120.0f;
        int bestIdx = -1;

        for (int i = 0; i < freqs.Length; i++)
        {
            if (MathF.Abs(freqs[i] - targetFreq) <= toleranceHz)
            {
                if (dbs[i] > maxDb)
                {
                    maxDb = dbs[i];
                    bestIdx = i;
                }
            }
        }

        if (bestIdx <= 0 || bestIdx >= freqs.Length - 1) return maxDb;

        // Interpolación parabólica de sub-bin
        float y1 = dbs[bestIdx - 1];
        float y2 = dbs[bestIdx];
        float y3 = dbs[bestIdx + 1];

        float denom = y1 - 2.0f * y2 + y3;
        if (MathF.Abs(denom) > 1e-5f)
        {
            float delta = 0.5f * (y1 - y3) / denom;
            float peakVal = y2 - 0.25f * (y1 - y3) * delta;
            return Math.Max(maxDb, peakVal);
        }

        return maxDb;
    }

    private static ImdResult EmptyResult(ImdStandard std) => new()
    {
        Standard = std,
        ImdPercent = 0.0f,
        ImdDb = -120.0f,
        PrimaryToneDb = -120.0f,
        Products = []
    };
}
