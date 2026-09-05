using System;

namespace SoundCalibrator.Core.Analysis;

public sealed class PowerCompressionResult
{
    public required float[] Frequencies { get; init; }
    public required float[] CompressionDb { get; init; }
    public required float BroadbandCompressionDb { get; init; }
    public required float MaxCompressionDb { get; init; }
    public required float WorstFrequencyHz { get; init; }
}

/// <summary>
/// Analiza y calcula la compresión dinámica de potencia (Power / Thermal Compression)
/// comparando una medición a bajo nivel (lineal) frente a una medición de alta excitación.
/// </summary>
public static class PowerCompressionCalculator
{
    public static PowerCompressionResult Calculate(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> baselineMagDb,
        ReadOnlySpan<float> highDriveMagDb,
        float driveLevelDeltaDb)
    {
        int count = Math.Min(frequencies.Length, Math.Min(baselineMagDb.Length, highDriveMagDb.Length));
        if (count == 0)
        {
            return new PowerCompressionResult
            {
                Frequencies = [],
                CompressionDb = [],
                BroadbandCompressionDb = 0f,
                MaxCompressionDb = 0f,
                WorstFrequencyHz = 0f
            };
        }

        float[] freqs = new float[count];
        float[] compDb = new float[count];

        float sumComp = 0.0f;
        int validPoints = 0;
        float maxComp = -100.0f;
        float worstFreq = 0.0f;

        for (int i = 0; i < count; i++)
        {
            freqs[i] = frequencies[i];
            float expectedDb = baselineMagDb[i] + driveLevelDeltaDb;
            float actualDb = highDriveMagDb[i];

            // Compresión = Esperado - Real (positivo = pérdida de energía)
            float compression = expectedDb - actualDb;
            compDb[i] = compression;

            if (frequencies[i] >= 20f && frequencies[i] <= 20000f)
            {
                sumComp += compression;
                validPoints++;

                if (compression > maxComp)
                {
                    maxComp = compression;
                    worstFreq = frequencies[i];
                }
            }
        }

        float avgComp = validPoints > 0 ? sumComp / validPoints : 0f;
        if (maxComp < -50f) maxComp = 0f;

        return new PowerCompressionResult
        {
            Frequencies = freqs,
            CompressionDb = compDb,
            BroadbandCompressionDb = avgComp,
            MaxCompressionDb = maxComp,
            WorstFrequencyHz = worstFreq
        };
    }
}
