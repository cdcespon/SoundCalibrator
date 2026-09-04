using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.DSP;

public sealed class DelayResult
{
    public int PeakIndex { get; set; }
    public float DelayMs { get; set; }
    public float DistanceMeters { get; set; }
    public float PeakMagnitude { get; set; }
}

public sealed class ImpulseResponseCalculator
{
    private readonly int _fftSize;
    private readonly FastFourierTransform _fft;
    private readonly float[] _spectrumReal;
    private readonly float[] _spectrumImag;
    private readonly float[] _timeDomainReal;
    private readonly float[] _timeDomainImag;

    public int FftSize => _fftSize;

    public ImpulseResponseCalculator(int fftSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        _fftSize = fftSize;
        _fft = new FastFourierTransform(fftSize);

        _spectrumReal = new float[fftSize];
        _spectrumImag = new float[fftSize];
        _timeDomainReal = new float[fftSize];
        _timeDomainImag = new float[fftSize];
    }

    /// <summary>
    /// Calcula la respuesta al impulso h(t) a partir de la magnitud (dB) y la fase (grados) de la función de transferencia.
    /// </summary>
    public void CalculateImpulseResponse(
        ReadOnlySpan<float> magnitudeDb,
        ReadOnlySpan<float> phaseDegrees,
        Span<float> impulseResponseOutput,
        float sampleRate,
        DelayResult delayResult)
    {
        int half = _fftSize / 2;
        int binCount = half + 1;

        // 1. Reconstruir espectro complejo bilateral conjugado
        for (int k = 0; k < binCount; k++)
        {
            float magLinear = MathF.Pow(10f, magnitudeDb[k] / 20f);
            float phaseRad = phaseDegrees[k] * (MathF.PI / 180f);

            float r = magLinear * MathF.Cos(phaseRad);
            float i = magLinear * MathF.Sin(phaseRad);

            _spectrumReal[k] = r;
            _spectrumImag[k] = i;

            if (k > 0 && k < half)
            {
                // Simetría conjugada para la mitad superior: H[N-k] = H*[k]
                _spectrumReal[_fftSize - k] = r;
                _spectrumImag[_fftSize - k] = -i;
            }
        }

        _spectrumReal.AsSpan().CopyTo(_timeDomainReal);
        _spectrumImag.AsSpan().CopyTo(_timeDomainImag);

        // 2. IFFT para obtener h(t)
        _fft.Inverse(_timeDomainReal, _timeDomainImag);

        // 3. Copiar a la salida
        int outLen = Math.Min(_fftSize, impulseResponseOutput.Length);
        _timeDomainReal.AsSpan(0, outLen).CopyTo(impulseResponseOutput);

        // 4. Detección del pico de retardo directo (buscar el máximo de |h(t)|)
        float maxVal = 0f;
        int peakIdx = 0;

        for (int n = 0; n < outLen; n++)
        {
            float absVal = MathF.Abs(_timeDomainReal[n]);
            if (absVal > maxVal)
            {
                maxVal = absVal;
                peakIdx = n;
            }
        }

        delayResult.PeakIndex = peakIdx;
        delayResult.PeakMagnitude = maxVal;
        delayResult.DelayMs = (float)peakIdx * 1000f / sampleRate;
        delayResult.DistanceMeters = delayResult.DelayMs * 0.343f; // 343 m/s a 20°C
    }
}
