using System;

namespace SoundCalibrator.Core.Calibration;

/// <summary>
/// Implementación estándar de las curvas de ponderación en frecuencia IEC 61672-1 (A-Weighting y C-Weighting).
/// </summary>
public static class AcousticWeighting
{
    private const double F1_2 = 20.598997 * 20.598997; // 424.3187
    private const double F2_2 = 107.65265 * 107.65265; // 11589.093
    private const double F3_2 = 737.86223 * 737.86223; // 544440.67
    private const double F4_2 = 12194.217 * 12194.217; // 148698928.0

    public static float GetAWeightingDb(float freqHz)
    {
        if (freqHz <= 0f) return -120f;

        double f2 = (double)freqHz * freqHz;
        double f4 = f2 * f2;

        double num = F4_2 * f4;
        double den = (f2 + F1_2) * Math.Sqrt((f2 + F2_2) * (f2 + F3_2)) * (f2 + F4_2);

        if (den < 1e-18) return -120f;
        double ra = num / den;

        return (float)(20.0 * Math.Log10(ra) + 2.0);
    }

    public static float GetCWeightingDb(float freqHz)
    {
        if (freqHz <= 0f) return -120f;

        double f2 = (double)freqHz * freqHz;
        double num = F4_2 * f2;
        double den = (f2 + F1_2) * (f2 + F4_2);

        if (den < 1e-18) return -120f;
        double rc = num / den;

        return (float)(20.0 * Math.Log10(rc) + 0.06);
    }
}
