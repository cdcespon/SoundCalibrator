using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Operations;

/// <summary>
/// Proporciona operaciones matemáticas y aritméticas de alto rendimiento (Zero Allocation) sobre trazas espectrales.
/// </summary>
public static class TraceMath
{
    public static void Subtract(ReadOnlySpan<float> minuendDb, ReadOnlySpan<float> subtrahendDb, Span<float> resultDb)
    {
        int count = Math.Min(minuendDb.Length, Math.Min(subtrahendDb.Length, resultDb.Length));
        for (int i = 0; i < count; i++)
        {
            resultDb[i] = minuendDb[i] - subtrahendDb[i];
        }
    }

    public static void CalculateDelta(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> measuredDb,
        TargetCurve target,
        Span<float> outDeltaDb,
        Span<float> outCorrectionDb = default)
    {
        int count = Math.Min(frequencies.Length, Math.Min(measuredDb.Length, outDeltaDb.Length));
        bool computeCorrection = !outCorrectionDb.IsEmpty;

        for (int i = 0; i < count; i++)
        {
            float targetDb = target.Evaluate(frequencies[i]);
            float delta = measuredDb[i] - targetDb;
            outDeltaDb[i] = delta;

            if (computeCorrection && i < outCorrectionDb.Length)
            {
                outCorrectionDb[i] = -delta;
            }
        }
    }

    /// <summary>
    /// Realiza la divisiÃ³n compleja entre dos trazas acÃºsticas (H_A / H_B).
    /// Calcula la funciÃ³n de transferencia relativa restando magnitudes en dB y fases angulares envueltas,
    /// combinando sus coherencias.
    /// </summary>
    public static AcousticTrace? DivideTraces(
        AcousticTrace numeratorTrace,
        AcousticTrace denominatorTrace,
        string resultTraceName = "Trace Diff (A/B)",
        string hexColor = "#E040FB")
    {
        if (numeratorTrace == null || denominatorTrace == null) return null;
        if (numeratorTrace.Frequencies.Length != denominatorTrace.Frequencies.Length)
        {
            throw new ArgumentException("Ambas trazas deben tener el mismo nÃºmero de bins de frecuencia.");
        }

        int count = numeratorTrace.Frequencies.Length;
        float[] freqs = (float[])numeratorTrace.Frequencies.Clone();
        float[] diffMagDb = new float[count];
        float[] diffPhaseDeg = new float[count];
        float[] diffCoh = new float[count];

        for (int i = 0; i < count; i++)
        {
            numeratorTrace.GetDisplayValues(i, out float magA, out float phaseA, out float cohA);
            denominatorTrace.GetDisplayValues(i, out float magB, out float phaseB, out float cohB);

            // En dB: A / B = MagA - MagB
            diffMagDb[i] = magA - magB;

            // En Fase: PhaseA - PhaseB envuelto a [-180, +180]
            float dPhase = phaseA - phaseB;
            diffPhaseDeg[i] = ((dPhase + 180f) % 360f + 360f) % 360f - 180f;

            // Coherencia combinada: CoherenceA * CoherenceB
            diffCoh[i] = Math.Clamp(cohA * cohB, 0f, 1f);
        }

        return new AcousticTrace(resultTraceName, hexColor, freqs, diffMagDb, diffPhaseDeg, diffCoh);
    }
}
