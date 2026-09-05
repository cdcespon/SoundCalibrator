using System;
using System.Collections.Generic;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Analysis;

/// <summary>
/// Simulador de Suma Acústica Compleja de Múltiples Fuentes (Sub + Main, L+R, etc.).
/// Modela la interferencia constructiva y destructiva real entre transductores considerando
/// magnitud, fase, compensación de delay, offsets de ganancia e inversión de polaridad.
/// </summary>
public static class AcousticSummationSimulator
{
    public static AcousticTrace? SimulateSummation(
        IReadOnlyList<AcousticTrace> traces,
        string sumTraceName = "Acoustic Sum",
        string hexColor = "#FF4081")
    {
        if (traces == null || traces.Count == 0)
        {
            return null;
        }

        if (traces.Count == 1)
        {
            var single = traces[0];
            int count = single.Frequencies.Length;
            float[] m = new float[count];
            float[] p = new float[count];
            float[] c = new float[count];

            for (int i = 0; i < count; i++)
            {
                single.GetDisplayValues(i, out m[i], out p[i], out c[i]);
            }

            return new AcousticTrace(sumTraceName, hexColor, single.Frequencies, m, p, c);
        }

        int binCount = traces[0].Frequencies.Length;
        for (int t = 1; t < traces.Count; t++)
        {
            if (traces[t].Frequencies.Length != binCount)
            {
                throw new ArgumentException("Todas las trazas a sumar deben tener idéntico número de bins de frecuencia.");
            }
        }

        float[] freqs = (float[])traces[0].Frequencies.Clone();
        float[] sumMagDb = new float[binCount];
        float[] sumPhaseDeg = new float[binCount];
        float[] sumCoh = new float[binCount];

        int n = traces.Count;

        for (int i = 0; i < binCount; i++)
        {
            double realSum = 0.0;
            double imagSum = 0.0;
            float cohMin = 1.0f;

            for (int t = 0; t < n; t++)
            {
                traces[t].GetDisplayValues(i, out float magDb, out float phaseDeg, out float coh);

                double linAmp = Math.Pow(10.0, magDb / 20.0);
                double rad = phaseDeg * (Math.PI / 180.0);

                realSum += linAmp * Math.Cos(rad);
                imagSum += linAmp * Math.Sin(rad);

                if (coh < cohMin) cohMin = coh;
            }

            double totalAmp = Math.Sqrt(realSum * realSum + imagSum * imagSum);

            sumMagDb[i] = totalAmp > 1e-6 ? (float)(20.0 * Math.Log10(totalAmp)) : -120f;
            sumPhaseDeg[i] = (float)(Math.Atan2(imagSum, realSum) * (180.0 / Math.PI));
            sumCoh[i] = cohMin;
        }

        return new AcousticTrace(sumTraceName, hexColor, freqs, sumMagDb, sumPhaseDeg, sumCoh);
    }
}
