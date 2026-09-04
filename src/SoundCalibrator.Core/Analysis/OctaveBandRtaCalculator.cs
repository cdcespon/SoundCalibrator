using System;

namespace SoundCalibrator.Core.Analysis;

public enum OctaveBandResolution
{
    FullOctave, // 1/1 Octava (10 bandas)
    ThirdOctave // 1/3 Octava (31 bandas)
}

public readonly record struct OctaveBand(
    float CenterFreqHz,
    float LowerFreqHz,
    float UpperFreqHz,
    float LevelDb);

/// <summary>
/// Analizador RTA por Bandas de Octava y Tercios de Octava normalizadas (ISO 266 / IEC 61260).
/// Integra la potencia acústica de los bins de la FFT en barras discretas de frecuencia.
/// </summary>
public static class OctaveBandRtaCalculator
{
    // Frecuencias centrales normalizadas ISO 266 (1/1 Octava: 10 bandas de 31.5Hz a 16kHz)
    public static readonly float[] FullOctaveCenters =
    [
        31.5f, 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f
    ];

    // Frecuencias centrales normalizadas ISO 266 (1/3 Octava: 31 bandas de 20Hz a 20kHz)
    public static readonly float[] ThirdOctaveCenters =
    [
        20f, 25f, 31.5f, 40f, 50f, 63f, 80f, 100f, 125f, 160f,
        200f, 250f, 315f, 400f, 500f, 630f, 800f, 1000f, 1250f, 1600f,
        2000f, 2500f, 3150f, 4000f, 5000f, 6300f, 8000f, 10000f, 12500f, 16000f, 20000f
    ];

    /// <summary>
    /// Calcula la potencia integrada en cada banda normalizada en base al espectro FFT en dBFS.
    /// destinationLevels debe tener longitud >= número de bandas seleccionadas.
    /// </summary>
    public static int CalculateBands(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> magnitudeDb,
        OctaveBandResolution resolution,
        Span<float> destinationLevels)
    {
        ReadOnlySpan<float> centers = resolution == OctaveBandResolution.FullOctave
            ? FullOctaveCenters
            : ThirdOctaveCenters;

        int bandCount = Math.Min(centers.Length, destinationLevels.Length);
        if (bandCount == 0 || frequencies.Length == 0 || magnitudeDb.Length == 0)
        {
            return 0;
        }

        // Factor de ancho de banda:
        // 1/1 octava: 2^(1/2) = 1.4142 => lower = fc / sqrt(2), upper = fc * sqrt(2)
        // 1/3 octava: 2^(1/6) = 1.122462 => lower = fc / 2^(1/6), upper = fc * 2^(1/6)
        float factor = resolution == OctaveBandResolution.FullOctave
            ? MathF.Pow(2.0f, 0.5f)
            : MathF.Pow(2.0f, 1.0f / 6.0f);

        int totalBins = Math.Min(frequencies.Length, magnitudeDb.Length);

        for (int b = 0; b < bandCount; b++)
        {
            float fc = centers[b];
            float fLower = fc / factor;
            float fUpper = fc * factor;

            double powerSum = 0.0;
            int binsInBand = 0;

            for (int i = 0; i < totalBins; i++)
            {
                float f = frequencies[i];
                if (f >= fLower && f <= fUpper)
                {
                    float db = magnitudeDb[i];
                    if (db > -120f)
                    {
                        powerSum += Math.Pow(10.0, db / 10.0);
                        binsInBand++;
                    }
                }
            }

            if (binsInBand > 0 && powerSum > 1e-12)
            {
                // Promedio de densidad espectral de potencia integrada en la banda
                destinationLevels[b] = (float)(10.0 * Math.Log10(powerSum));
            }
            else
            {
                destinationLevels[b] = -96.0f; // Piso de escala dBFS
            }
        }

        return bandCount;
    }
}
