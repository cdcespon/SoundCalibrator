using System;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct ThdResult(
    float FundamentalFreqHz,
    float FundamentalDb,
    float ThdPercent,
    float ThdDb,
    float ThdPlusNPercent,
    float ThdPlusNDb,
    float[] HarmonicDbs);

public static class ThdCalculator
{
    public static ThdResult Calculate(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> magnitudeDb,
        int maxHarmonics = 5,
        float minSearchFreq = 20f,
        float maxSearchFreq = 10000f)
    {
        int count = Math.Min(frequencies.Length, magnitudeDb.Length);
        if (count == 0)
        {
            return new ThdResult(0, -120f, 0, -120f, 0, -120f, []);
        }

        // 1. Encontrar la frecuencia fundamental (pico principal)
        int fundIdx = -1;
        float maxMag = -200f;

        for (int i = 0; i < count; i++)
        {
            float f = frequencies[i];
            if (f >= minSearchFreq && f <= maxSearchFreq)
            {
                if (magnitudeDb[i] > maxMag)
                {
                    maxMag = magnitudeDb[i];
                    fundIdx = i;
                }
            }
        }

        if (fundIdx < 0 || maxMag < -100f)
        {
            return new ThdResult(0, maxMag, 0, -120f, 0, -120f, []);
        }

        float fundFreq = frequencies[fundIdx];
        float fundDb = magnitudeDb[fundIdx];
        float fundPower = MathF.Pow(10f, fundDb / 10f);

        // 2. Extraer armónicos (2do a N-ésimo)
        float harmPowerSum = 0f;
        float[] harmonicDbs = new float[maxHarmonics - 1];

        for (int h = 2; h <= maxHarmonics; h++)
        {
            float targetHarmFreq = h * fundFreq;
            if (targetHarmFreq > frequencies[^1])
            {
                harmonicDbs[h - 2] = -120f;
                continue;
            }

            // Buscar pico local alrededor de la frecuencia armónica (+/- 5%)
            int bestIdx = -1;
            float bestMag = -200f;

            for (int i = 0; i < count; i++)
            {
                float f = frequencies[i];
                if (f >= targetHarmFreq * 0.95f && f <= targetHarmFreq * 1.05f)
                {
                    if (magnitudeDb[i] > bestMag)
                    {
                        bestMag = magnitudeDb[i];
                        bestIdx = i;
                    }
                }
            }

            if (bestIdx >= 0)
            {
                harmonicDbs[h - 2] = bestMag;
                harmPowerSum += MathF.Pow(10f, bestMag / 10f);
            }
            else
            {
                harmonicDbs[h - 2] = -120f;
            }
        }

        // 3. Potencia total en banda audible
        float totalPower = 0f;
        for (int i = 0; i < count; i++)
        {
            if (frequencies[i] >= 20f && frequencies[i] <= 20000f)
            {
                totalPower += MathF.Pow(10f, magnitudeDb[i] / 10f);
            }
        }

        // 4. Ratios THD y THD+N
        float thdRatio = MathF.Sqrt(MathF.Max(0f, harmPowerSum) / MathF.Max(1e-12f, fundPower));
        float thdPercent = thdRatio * 100f;
        float thdDb = 10f * MathF.Log10(MathF.Max(1e-12f, harmPowerSum / MathF.Max(1e-12f, fundPower)));

        float noiseAndHarmPower = MathF.Max(0f, totalPower - fundPower);
        float thdPlusNRatio = MathF.Sqrt(noiseAndHarmPower / MathF.Max(1e-12f, totalPower));
        float thdPlusNPercent = thdPlusNRatio * 100f;
        float thdPlusNDb = 10f * MathF.Log10(MathF.Max(1e-12f, noiseAndHarmPower / MathF.Max(1e-12f, totalPower)));

        return new ThdResult(
            fundFreq,
            fundDb,
            thdPercent,
            thdDb,
            thdPlusNPercent,
            thdPlusNDb,
            harmonicDbs);
    }
}
