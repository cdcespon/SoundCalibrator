using System;
using SoundCalibrator.Core.DSP;

namespace SoundCalibrator.Core.Analysis;

public sealed class MinimumPhaseResult
{
    public required float[] Frequencies { get; init; }
    public required float[] MinPhaseDegrees { get; init; }
    public required float[] ExcessPhaseDegrees { get; init; }
    public required float[] ExcessGroupDelayMs { get; init; }
}

/// <summary>
/// Descompone una función de transferencia acústica en sus componentes de Fase Mínima (Minimum Phase)
/// y Fase de Exceso (Excess Phase / Excess Group Delay) mediante el cálculo del cepstrum real,
/// aislando el retardo acústico de propagación y las reflexiones no-mínimas de sala.
/// </summary>
public static class MinimumPhaseAnalyzer
{
    private const float Ln10Over20 = 0.1151292546497f; // MathF.Log(10.0f) / 20.0f

    public static MinimumPhaseResult Analyze(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> magnitudeDb,
        ReadOnlySpan<float> phaseDegrees)
    {
        int count = Math.Min(frequencies.Length, Math.Min(magnitudeDb.Length, phaseDegrees.Length));
        if (count < 2)
        {
            return new MinimumPhaseResult
            {
                Frequencies = [],
                MinPhaseDegrees = [],
                ExcessPhaseDegrees = [],
                ExcessGroupDelayMs = []
            };
        }

        // El espectro simétrico de dos lados requiere tamaño N = 2 * (count - 1)
        int rawN = 2 * (count - 1);
        int fftSize = 1;
        while (fftSize < rawN) fftSize <<= 1;

        float[] real = new float[fftSize];
        float[] imag = new float[fftSize];

        // 1. Construir log-magnitud simétrica en el dominio frecuencial
        for (int i = 0; i < count; i++)
        {
            float logMag = magnitudeDb[i] * Ln10Over20;
            real[i] = logMag;
            if (i > 0 && i < count - 1)
            {
                int mirror = fftSize - i;
                if (mirror < fftSize)
                {
                    real[mirror] = logMag;
                }
            }
        }

        // 2. IFFT para obtener el cepstrum real c[n]
        var fft = new FastFourierTransform(fftSize);
        fft.Inverse(real, imag);

        // 3. Ventana causal de liftering para fase mínima:
        // n = 0: se mantiene
        // 1 <= n < fftSize / 2: multiplicar por 2
        // n = fftSize / 2: se mantiene
        // n > fftSize / 2: anular a cero
        int half = fftSize / 2;
        for (int n = 1; n < half; n++)
        {
            real[n] *= 2.0f;
            imag[n] = 0.0f;
        }

        imag[0] = 0.0f;
        imag[half] = 0.0f;

        for (int n = half + 1; n < fftSize; n++)
        {
            real[n] = 0.0f;
            imag[n] = 0.0f;
        }

        // 4. FFT directa para recuperar el espectro analítico complejo ln H_min(f)
        fft.Forward(real, imag);

        // 5. Extraer fase mínima en grados y fase de exceso
        float[] freqs = new float[count];
        float[] minPhase = new float[count];
        float[] excessPhase = new float[count];
        float[] excessGd = new float[count];

        const float radToDeg = 180.0f / MathF.PI;

        for (int i = 0; i < count; i++)
        {
            freqs[i] = frequencies[i];

            // La parte imaginaria de ln H_min(f) es la fase mínima en radianes
            float mp = imag[i] * radToDeg;
            minPhase[i] = WrapDegrees(mp);

            // Fase de exceso: Fase Medida - Fase Mínima
            float ep = phaseDegrees[i] - minPhase[i];
            excessPhase[i] = WrapDegrees(ep);
        }

        // 6. Retardo de Grupo de Exceso (Excess Group Delay): tau_excess = -1/360 * d(excessPhase)/df
        for (int i = 1; i < count - 1; i++)
        {
            float df = frequencies[i + 1] - frequencies[i - 1];
            if (df > 1e-4f)
            {
                float dPhase = excessPhase[i + 1] - excessPhase[i - 1];
                // Desenrollar salto si cruza +/-180
                while (dPhase > 180f) dPhase -= 360f;
                while (dPhase < -180f) dPhase += 360f;

                // En milisegundos: (-1 / 360) * (dPhase / df) * 1000
                excessGd[i] = -(dPhase / df) * (1000.0f / 360.0f);
            }
        }
        excessGd[0] = excessGd[1];
        excessGd[count - 1] = excessGd[count - 2];

        return new MinimumPhaseResult
        {
            Frequencies = freqs,
            MinPhaseDegrees = minPhase,
            ExcessPhaseDegrees = excessPhase,
            ExcessGroupDelayMs = excessGd
        };
    }

    private static float WrapDegrees(float deg)
    {
        return ((deg + 180.0f) % 360.0f + 360.0f) % 360.0f - 180.0f;
    }
}
