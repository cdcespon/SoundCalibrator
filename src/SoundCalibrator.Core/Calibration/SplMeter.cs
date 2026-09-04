using System;

namespace SoundCalibrator.Core.Calibration;

public readonly record struct SplReading(
    float DbZ,
    float DbA,
    float DbC,
    float PeakDbZ);

/// <summary>
/// Sonómetro y medidor acústico de presión sonora (SPL) en dBZ, dBA y dBC con calibración de pistófono.
/// </summary>
public sealed class SplMeter
{
    public float SplOffsetDb { get; set; } = 120.0f;

    public void CalibrateWithTone(float measuredDbFsAt1kHz, float calibratorLevelDbSpl = 94.0f)
    {
        SplOffsetDb = calibratorLevelDbSpl - measuredDbFsAt1kHz;
    }

    public SplReading CalculateSpl(ReadOnlySpan<float> frequencies, ReadOnlySpan<float> magnitudeDbFs)
    {
        int count = Math.Min(frequencies.Length, magnitudeDbFs.Length);
        if (count == 0)
        {
            return new SplReading(0f, 0f, 0f, 0f);
        }

        double sumZ = 0.0;
        double sumA = 0.0;
        double sumC = 0.0;
        float maxDbZ = -200f;

        for (int i = 0; i < count; i++)
        {
            float f = frequencies[i];
            if (f < 20f || f > 20000f) continue;

            float mag = magnitudeDbFs[i];
            if (mag > maxDbZ) maxDbZ = mag;

            double pZ = Math.Pow(10.0, mag / 10.0);
            sumZ += pZ;

            float wA = AcousticWeighting.GetAWeightingDb(f);
            sumA += Math.Pow(10.0, (mag + wA) / 10.0);

            float wC = AcousticWeighting.GetCWeightingDb(f);
            sumC += Math.Pow(10.0, (mag + wC) / 10.0);
        }

        float dbZ = (float)(10.0 * Math.Log10(Math.Max(1e-12, sumZ))) + SplOffsetDb;
        float dbA = (float)(10.0 * Math.Log10(Math.Max(1e-12, sumA))) + SplOffsetDb;
        float dbC = (float)(10.0 * Math.Log10(Math.Max(1e-12, sumC))) + SplOffsetDb;
        float peakSpl = maxDbZ + SplOffsetDb;

        return new SplReading(dbZ, dbA, dbC, peakSpl);
    }
}
