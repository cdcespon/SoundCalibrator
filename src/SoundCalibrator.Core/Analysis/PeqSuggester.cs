using System;
using System.Collections.Generic;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct PeqFilterSuggestion(
    float FrequencyHz,
    float GainDb,
    float Q,
    float BandwidthOctaves);

public static class PeqSuggester
{
    public static IReadOnlyList<PeqFilterSuggestion> SuggestFilters(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> deltaDb,
        int maxFilters = 5,
        float minDbThreshold = 2.0f)
    {
        int count = Math.Min(frequencies.Length, deltaDb.Length);
        if (count < 3 || maxFilters <= 0)
        {
            return Array.Empty<PeqFilterSuggestion>();
        }

        var candidates = new List<PeqFilterSuggestion>();

        for (int i = 1; i < count - 1; i++)
        {
            float f = frequencies[i];
            if (f < 20f || f > 18000f) continue;

            float val = deltaDb[i];
            float prev = deltaDb[i - 1];
            float next = deltaDb[i + 1];

            bool isPeak = val >= minDbThreshold && val >= prev && val >= next;
            bool isDip = val <= -minDbThreshold && val <= prev && val <= next;

            if (isPeak || isDip)
            {
                float peakErr = val;
                float halfLevel = peakErr * 0.707f;

                // Buscar ancho de banda a nivel medio
                float fLow = f * 0.85f;
                for (int j = i - 1; j >= 0; j--)
                {
                    if ((isPeak && deltaDb[j] <= halfLevel) || (isDip && deltaDb[j] >= halfLevel))
                    {
                        fLow = frequencies[j];
                        break;
                    }
                }

                float fHigh = f * 1.15f;
                for (int j = i + 1; j < count; j++)
                {
                    if ((isPeak && deltaDb[j] <= halfLevel) || (isDip && deltaDb[j] >= halfLevel))
                    {
                        fHigh = frequencies[j];
                        break;
                    }
                }

                float bw = Math.Max(1f, fHigh - fLow);
                float q = Math.Clamp(f / bw, 0.5f, 20.0f);
                float bwOct = MathF.Log(Math.Max(1.01f, fHigh / Math.Max(1f, fLow))) / MathF.Log(2f);

                // Ganancia correctiva de compensación
                float corrGain = -peakErr;

                candidates.Add(new PeqFilterSuggestion(f, corrGain, q, bwOct));
            }
        }

        // Ordenar candidatos por magnitud de corrección (picos más prominentes primero)
        candidates.Sort((a, b) => MathF.Abs(b.GainDb).CompareTo(MathF.Abs(a.GainDb)));

        // Filtrar filtros demasiado cercanos entre sí (mínimo 1/6 de octava de separación)
        var selected = new List<PeqFilterSuggestion>();
        foreach (var cand in candidates)
        {
            if (selected.Count >= maxFilters) break;

            bool tooClose = false;
            foreach (var sel in selected)
            {
                float ratio = Math.Max(cand.FrequencyHz, sel.FrequencyHz) / Math.Min(cand.FrequencyHz, sel.FrequencyHz);
                if (ratio < 1.12f) // ~1/6 octava
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                selected.Add(cand);
            }
        }

        return selected;
    }
}
