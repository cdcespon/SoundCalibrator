using System;
using System.Collections.Generic;
using SoundCalibrator.Core.DSP;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct AcousticReflection(
    float TimeMs,
    float RelativeDelayMs,
    float LevelDb,
    float PathDifferenceMeters);

public sealed class EtcResult
{
    public required float[] TimeMs { get; init; }
    public required float[] EnvelopeDb { get; init; }
    public required float DirectSoundTimeMs { get; init; }
    public required IReadOnlyList<AcousticReflection> Reflections { get; init; }
}

/// <summary>
/// Calcula la Curva de Energía-Tiempo (ETC / Energy Time Curve) mediante la transformada de Hilbert (señal analítica),
/// identificando la llegada del sonido directo y las reflexiones tempranas (early reflections) con su diferencia de recorrido acústico.
/// </summary>
public static class EtcCalculator
{
    private const float SpeedOfSoundMps = 343.0f; // 20°C

    public static EtcResult Calculate(
        ReadOnlySpan<float> impulseResponse,
        int sampleRate,
        float minDb = -80.0f,
        float reflectionThresholdDb = -30.0f,
        float minPeakSpacingMs = 0.5f)
    {
        if (impulseResponse.IsEmpty || sampleRate <= 0)
        {
            return new EtcResult
            {
                TimeMs = [],
                EnvelopeDb = [],
                DirectSoundTimeMs = 0f,
                Reflections = []
            };
        }

        int n = impulseResponse.Length;
        int fftSize = 1;
        while (fftSize < n) fftSize <<= 1;

        float[] real = new float[fftSize];
        float[] imag = new float[fftSize];
        impulseResponse.CopyTo(real);

        // 1. FFT directa
        var fft = new FastFourierTransform(fftSize);
        fft.Forward(real, imag);

        // 2. Señal analítica en frecuencia:
        // Z[0] y Z[half] se mantienen
        // 1 <= k < half: multiplicar por 2
        // k > half: anular a 0
        int half = fftSize / 2;
        for (int k = 1; k < half; k++)
        {
            real[k] *= 2.0f;
            imag[k] *= 2.0f;
        }

        for (int k = half + 1; k < fftSize; k++)
        {
            real[k] = 0.0f;
            imag[k] = 0.0f;
        }

        // 3. IFFT -> z(t) = x(t) + j*H{x(t)}
        fft.Inverse(real, imag);

        // 4. Envolvente instantánea |z(t)|
        float[] timeMs = new float[n];
        float[] env = new float[n];
        float maxEnv = 1e-12f;
        int directIndex = 0;

        float dtMs = 1000.0f / sampleRate;
        for (int i = 0; i < n; i++)
        {
            timeMs[i] = i * dtMs;
            float mag = MathF.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            env[i] = mag;
            if (mag > maxEnv)
            {
                maxEnv = mag;
                directIndex = i;
            }
        }

        // 5. Escala Logarítmica normalizada (0 dB = pico de sonido directo)
        float[] envDb = new float[n];
        for (int i = 0; i < n; i++)
        {
            float ratio = env[i] / maxEnv;
            float db = ratio > 1e-5f ? 20.0f * MathF.Log10(ratio) : minDb;
            envDb[i] = Math.Max(db, minDb);
        }

        float directTimeMs = timeMs[directIndex];

        // 6. Detector de reflexiones tempranas destacadas con supresión de no-máximos
        int deadZoneSamples = (int)MathF.Ceiling(minPeakSpacingMs * sampleRate / 1000.0f);
        int startSearch = Math.Min(n - 1, directIndex + deadZoneSamples);

        var candidatePeaks = new List<AcousticReflection>();
        for (int i = startSearch; i < n - 1; i++)
        {
            float current = envDb[i];
            if (current >= reflectionThresholdDb && current > envDb[i - 1] && current >= envDb[i + 1])
            {
                float delayMs = timeMs[i] - directTimeMs;
                float distMeters = delayMs * 0.001f * SpeedOfSoundMps;

                candidatePeaks.Add(new AcousticReflection(
                    TimeMs: timeMs[i],
                    RelativeDelayMs: delayMs,
                    LevelDb: current,
                    PathDifferenceMeters: distMeters));
            }
        }

        // Supresión de no-máximos dentro de minPeakSpacingMs
        var reflections = new List<AcousticReflection>();
        for (int i = 0; i < candidatePeaks.Count; i++)
        {
            var p = candidatePeaks[i];
            bool isHighest = true;
            for (int j = 0; j < candidatePeaks.Count; j++)
            {
                if (i == j) continue;
                if (MathF.Abs(candidatePeaks[j].TimeMs - p.TimeMs) <= minPeakSpacingMs)
                {
                    if (candidatePeaks[j].LevelDb > p.LevelDb)
                    {
                        isHighest = false;
                        break;
                    }
                }
            }

            if (isHighest)
            {
                bool duplicate = false;
                for (int k = 0; k < reflections.Count; k++)
                {
                    if (MathF.Abs(reflections[k].TimeMs - p.TimeMs) <= minPeakSpacingMs)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    reflections.Add(p);
                }
            }
        }

        return new EtcResult
        {
            TimeMs = timeMs,
            EnvelopeDb = envDb,
            DirectSoundTimeMs = directTimeMs,
            Reflections = reflections
        };
    }
}
