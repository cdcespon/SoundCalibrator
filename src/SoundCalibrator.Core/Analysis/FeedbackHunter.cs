using System;
using System.Collections.Generic;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct FeedbackCandidate(
    float FrequencyHz,
    float LevelDb,
    float ProminenceDb,
    float Q);

/// <summary>
/// Detector de acoples acústicos y resonancias parásitas de alta Q (Feedback Hunter)
/// para sistemas de sonido en vivo y monitoreo de escenario.
/// </summary>
public static class FeedbackHunter
{
    public static IReadOnlyList<FeedbackCandidate> Detect(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> magnitudeDb,
        float prominenceThresholdDb = 12.0f,
        float minQ = 5.0f,
        int maxResults = 3)
    {
        int count = Math.Min(frequencies.Length, magnitudeDb.Length);
        if (count < 10 || maxResults <= 0)
        {
            return Array.Empty<FeedbackCandidate>();
        }

        var candidates = new List<FeedbackCandidate>();

        for (int i = 2; i < count - 2; i++)
        {
            float f = frequencies[i];
            if (f < 80f || f > 16000f) continue;

            float val = magnitudeDb[i];
            if (val < -65f) continue; // Ignorar señales sumamente bajas

            // Comprobar si es un pico local estricto
            if (val > magnitudeDb[i - 1] && val > magnitudeDb[i + 1])
            {
                // Calcular piso de ruido local promediando bins circundantes
                int window = 12;
                int start = Math.Max(0, i - window);
                int end = Math.Min(count - 1, i + window);

                double sumBg = 0.0;
                int bgCount = 0;

                for (int j = start; j <= end; j++)
                {
                    // Excluir los 2 bins inmediatamente adyacentes al pico
                    if (Math.Abs(j - i) > 1)
                    {
                        sumBg += magnitudeDb[j];
                        bgCount++;
                    }
                }

                if (bgCount < 4) continue;
                float bgLevel = (float)(sumBg / bgCount);
                float prominence = val - bgLevel;

                if (prominence < prominenceThresholdDb) continue;

                // Calcular ancho de banda a -3 dB del pico para determinar Q
                float halfLevel = val - 3.0f;

                float fLow = frequencies[Math.Max(0, i - 1)];
                for (int j = i - 1; j >= start; j--)
                {
                    if (magnitudeDb[j] <= halfLevel)
                    {
                        fLow = frequencies[j];
                        break;
                    }
                }

                float fHigh = frequencies[Math.Min(count - 1, i + 1)];
                for (int j = i + 1; j <= end; j++)
                {
                    if (magnitudeDb[j] <= halfLevel)
                    {
                        fHigh = frequencies[j];
                        break;
                    }
                }

                float bw = Math.Max(1f, fHigh - fLow);
                float q = f / bw;

                if (q >= minQ)
                {
                    candidates.Add(new FeedbackCandidate(f, val, prominence, q));
                }
            }
        }

        // Ordenar por prominencia descendente
        candidates.Sort((a, b) => b.ProminenceDb.CompareTo(a.ProminenceDb));

        if (candidates.Count > maxResults)
        {
            return candidates.GetRange(0, maxResults);
        }

        return candidates;
    }
}
