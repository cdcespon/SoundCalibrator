using System;

namespace SoundCalibrator.Core.DSP;

/// <summary>
/// Frequency-Dependent Windowing (FDW) processor.
/// Applies a frequency-adaptive time window (proportional to wave periods / cycles)
/// centered at the direct sound peak of an impulse response h(t) to remove early
/// and late acoustic reflections, yielding a quasi-anechoic transfer function.
/// </summary>
public static class FrequencyDependentWindow
{
    /// <summary>
    /// Applies Frequency-Dependent Windowing to an impulse response and computes the quasi-anechoic magnitude response in dB.
    /// </summary>
    /// <param name="impulseResponse">Raw impulse response h(t).</param>
    /// <param name="frequencies">Frequencies at which to evaluate the FDW response.</param>
    /// <param name="outputMagnitudeDb">Output span receiving the quasi-anechoic magnitude in dB.</param>
    /// <param name="cycles">Number of cycles (periods) to retain per frequency (typically 5 to 15 cycles, default 10).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public static void ApplyFdw(
        ReadOnlySpan<float> impulseResponse,
        ReadOnlySpan<float> frequencies,
        Span<float> outputMagnitudeDb,
        float cycles = 10.0f,
        float sampleRate = 48000.0f)
    {
        if (impulseResponse.Length == 0)
            throw new ArgumentException("Impulse response cannot be empty.", nameof(impulseResponse));
        if (frequencies.Length != outputMagnitudeDb.Length)
            throw new ArgumentException("Frequencies and outputMagnitudeDb spans must have identical length.");
        if (cycles <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cycles), "Cycles must be greater than zero.");
        if (sampleRate <= 0f)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        // 1. Encuentra el índice del sonido directo (pico de energía absoluta)
        int peakIdx = 0;
        float maxAbs = 0.0f;
        for (int i = 0; i < impulseResponse.Length; i++)
        {
            float absVal = MathF.Abs(impulseResponse[i]);
            if (absVal > maxAbs)
            {
                maxAbs = absVal;
                peakIdx = i;
            }
        }

        if (maxAbs < 1e-12f)
        {
            outputMagnitudeDb.Fill(-120.0f);
            return;
        }

        float twoPi = 2.0f * MathF.PI;
        float invSampleRate = 1.0f / sampleRate;

        // 2. Evalúa cada frecuencia con ventana adaptativa en ciclos
        for (int f = 0; f < frequencies.Length; f++)
        {
            float freq = frequencies[f];
            if (freq <= 1.0f)
            {
                outputMagnitudeDb[f] = -120.0f;
                continue;
            }

            // Duración del período T = 1 / freq
            // Semi-ancho de la ventana en muestras = (cycles / 2) * T * sampleRate
            float halfWindowSamples = (cycles * 0.5f / freq) * sampleRate;
            int halfWinInt = Math.Max(2, (int)MathF.Ceiling(halfWindowSamples));

            int startIdx = Math.Max(0, peakIdx - halfWinInt);
            int endIdx = Math.Min(impulseResponse.Length - 1, peakIdx + halfWinInt);

            float omega = twoPi * freq * invSampleRate;
            float sumReal = 0.0f;
            float sumImag = 0.0f;
            float invHalfWin = 1.0f / halfWinInt;

            // DTFT con ventana de Hann simétrica centrada en peakIdx
            for (int n = startIdx; n <= endIdx; n++)
            {
                float h = impulseResponse[n];
                // Hann window: 0.5 * (1 + cos(pi * (n - peakIdx) / halfWinInt))
                float dist = (n - peakIdx) * invHalfWin;
                float win = 0.5f * (1.0f + MathF.Cos(MathF.PI * dist));

                float val = h * win;
                // Referenciado a peakIdx para fase lineal limpia: e^(-j * omega * (n - peakIdx))
                float phase = omega * (n - peakIdx);
                sumReal += val * MathF.Cos(phase);
                sumImag -= val * MathF.Sin(phase);
            }

            float magSquared = sumReal * sumReal + sumImag * sumImag;
            float mag = MathF.Sqrt(magSquared);
            outputMagnitudeDb[f] = 20.0f * MathF.Log10(MathF.Max(mag, 1e-6f));
        }
    }
}
