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
}
