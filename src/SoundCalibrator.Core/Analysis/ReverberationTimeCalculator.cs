using System;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct ReverberationTimeResult(
    bool IsValid,
    float EdtSeconds,
    float T20Seconds,
    float T30Seconds,
    float PeakTimeMs,
    float DynamicRangeDb);

/// <summary>
/// Calcula los tiempos de reverberación acústica estándar (ISO 3382) mediante integración hacia atrás de Schroeder.
/// </summary>
public static class ReverberationTimeCalculator
{
    public static ReverberationTimeResult Calculate(ReadOnlySpan<float> impulseResponse, int sampleRate)
    {
        int length = impulseResponse.Length;
        if (length < 128 || sampleRate <= 0)
        {
            return new ReverberationTimeResult(false, 0f, 0f, 0f, 0f, 0f);
        }

        // 1. Encontrar el pico del impulso
        int peakIdx = 0;
        float maxVal = 0f;
        for (int i = 0; i < length; i++)
        {
            float abs = MathF.Abs(impulseResponse[i]);
            if (abs > maxVal)
            {
                maxVal = abs;
                peakIdx = i;
            }
        }

        if (maxVal < 1e-6f)
        {
            return new ReverberationTimeResult(false, 0f, 0f, 0f, 0f, 0f);
        }

        // 2. Integración hacia atrás de Schroeder: E(t) = Sum(h^2(tau)) desde t hasta final
        // Pre-alocamos buffer temporal local en pila o arreglo para la curva EDC
        float[] edcDb = new float[length - peakIdx];
        double sumEnergy = 0.0;

        for (int i = length - 1; i >= peakIdx; i--)
        {
            float s = impulseResponse[i];
            sumEnergy += s * s;
            edcDb[i - peakIdx] = (float)sumEnergy;
        }

        float totalEnergy = edcDb[0];
        if (totalEnergy < 1e-12f)
        {
            return new ReverberationTimeResult(false, 0f, 0f, 0f, 0f, 0f);
        }

        float logTotal = MathF.Log10(totalEnergy);
        float minEdc = 0f;

        for (int i = 0; i < edcDb.Length; i++)
        {
            if (edcDb[i] > 1e-12f)
            {
                edcDb[i] = 10f * (MathF.Log10(edcDb[i]) - logTotal);
            }
            else
            {
                edcDb[i] = -120f;
            }
            if (edcDb[i] < minEdc) minEdc = edcDb[i];
        }

        float dynamicRange = MathF.Abs(minEdc);
        float peakTimeMs = (float)peakIdx * 1000f / sampleRate;

        // 3. Regresión lineal para EDT (0 dB a -10 dB)
        float edt = FitSlope(edcDb, -10f, 0f, sampleRate);

        // 4. Regresión lineal para T20 (-5 dB a -25 dB)
        float t20 = FitSlope(edcDb, -25f, -5f, sampleRate);

        // 5. Regresión lineal para T30 (-5 dB a -35 dB)
        float t30 = FitSlope(edcDb, -35f, -5f, sampleRate);

        bool isValid = t20 > 0.01f || edt > 0.01f;

        return new ReverberationTimeResult(
            isValid,
            edt,
            t20 > 0f ? t20 : edt,
            t30 > 0f ? t30 : (t20 > 0f ? t20 : edt),
            peakTimeMs,
            dynamicRange);
    }

    private static float FitSlope(float[] edcDb, float minDb, float maxDb, int sampleRate)
    {
        double sumT = 0;
        double sumY = 0;
        double sumT2 = 0;
        double sumTY = 0;
        int n = 0;

        for (int i = 0; i < edcDb.Length; i++)
        {
            float y = edcDb[i];
            if (y >= minDb && y <= maxDb)
            {
                double t = (double)i / sampleRate;
                sumT += t;
                sumY += y;
                sumT2 += t * t;
                sumTY += t * y;
                n++;
            }
        }

        if (n < 10) return 0f;

        double denom = (n * sumT2) - (sumT * sumT);
        if (Math.Abs(denom) < 1e-12) return 0f;

        double slope = ((n * sumTY) - (sumT * sumY)) / denom;
        if (slope >= -0.05) return 0f; // La curva debe decrecer

        // Tiempo para decaer 60 dB
        return (float)(-60.0 / slope);
    }
}
