using System;
using System.Collections.Generic;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Averaging;

public enum SpatialAverageMode
{
    Power,
    CoherenceWeightedPower,
    ComplexVector
}

/// <summary>
/// Promediador espacial de múltiples micrófonos y mediciones (Spatial Mic Averaging).
/// Permite combinar mediciones de distintas posiciones de audiencia (FOH, Balcón, Platea, etc.)
/// en una curva acústica unificada representativa del recinto.
/// </summary>
public static class SpatialAverager
{
    public static AcousticTrace? CalculateSpatialAverage(
        IReadOnlyList<AcousticTrace> traces,
        SpatialAverageMode mode = SpatialAverageMode.Power,
        string averageTraceName = "Spatial Average",
        string hexColor = "#00E5FF")
    {
        if (traces == null || traces.Count == 0)
        {
            return null;
        }

        if (traces.Count == 1)
        {
            var single = traces[0];
            return new AcousticTrace(
                averageTraceName,
                hexColor,
                single.Frequencies,
                single.MagnitudeDb,
                single.PhaseDegrees,
                single.Coherence);
        }

        int binCount = traces[0].Frequencies.Length;
        // Validar longitudes consistentes
        for (int t = 1; t < traces.Count; t++)
        {
            if (traces[t].Frequencies.Length != binCount)
            {
                throw new ArgumentException("Todas las trazas deben tener el mismo número de bins de frecuencia.");
            }
        }

        float[] freqs = (float[])traces[0].Frequencies.Clone();
        float[] avgMagDb = new float[binCount];
        float[] avgPhaseDeg = new float[binCount];
        float[] avgCoh = new float[binCount];

        int n = traces.Count;

        for (int i = 0; i < binCount; i++)
        {
            float cohSum = 0f;
            for (int t = 0; t < n; t++)
            {
                cohSum += traces[t].Coherence[i];
            }
            avgCoh[i] = cohSum / n;

            switch (mode)
            {
                case SpatialAverageMode.Power:
                {
                    double powerSum = 0.0;
                    for (int t = 0; t < n; t++)
                    {
                        float mag = traces[t].MagnitudeDb[i];
                        powerSum += Math.Pow(10.0, mag / 10.0);
                    }
                    double avgPower = powerSum / n;
                    avgMagDb[i] = avgPower > 1e-12 ? (float)(10.0 * Math.Log10(avgPower)) : -120f;
                    avgPhaseDeg[i] = 0f; // La fase no tiene significado físico en promedio de potencia pura
                    break;
                }

                case SpatialAverageMode.CoherenceWeightedPower:
                {
                    double weightedPowerSum = 0.0;
                    double weightSum = 0.0;

                    for (int t = 0; t < n; t++)
                    {
                        float mag = traces[t].MagnitudeDb[i];
                        float weight = Math.Max(1e-4f, traces[t].Coherence[i]);
                        double power = Math.Pow(10.0, mag / 10.0);

                        weightedPowerSum += weight * power;
                        weightSum += weight;
                    }

                    double avgPower = weightSum > 0.0 ? weightedPowerSum / weightSum : 0.0;
                    avgMagDb[i] = avgPower > 1e-12 ? (float)(10.0 * Math.Log10(avgPower)) : -120f;
                    avgPhaseDeg[i] = 0f;
                    break;
                }

                case SpatialAverageMode.ComplexVector:
                {
                    double realSum = 0.0;
                    double imagSum = 0.0;

                    for (int t = 0; t < n; t++)
                    {
                        float magDb = traces[t].MagnitudeDb[i];
                        float phaseDeg = traces[t].PhaseDegrees[i];

                        double linAmp = Math.Pow(10.0, magDb / 20.0);
                        double rad = phaseDeg * (Math.PI / 180.0);

                        realSum += linAmp * Math.Cos(rad);
                        imagSum += linAmp * Math.Sin(rad);
                    }

                    double avgReal = realSum / n;
                    double avgImag = imagSum / n;
                    double avgAmp = Math.Sqrt(avgReal * avgReal + avgImag * avgImag);

                    avgMagDb[i] = avgAmp > 1e-12 ? (float)(20.0 * Math.Log10(avgAmp)) : -120f;
                    avgPhaseDeg[i] = (float)(Math.Atan2(avgImag, avgReal) * (180.0 / Math.PI));
                    break;
                }
            }
        }

        return new AcousticTrace(averageTraceName, hexColor, freqs, avgMagDb, avgPhaseDeg, avgCoh);
    }
}
