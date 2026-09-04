using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.DSP;

public sealed class TransferFunctionCalculator
{
    private readonly int _fftSize;
    private readonly FastFourierTransform _fft;
    private readonly float[] _window;

    // Scratch buffers pre-asignados para CERO allocs en el bucle en caliente
    private readonly float[] _refReal;
    private readonly float[] _refImag;
    private readonly float[] _measReal;
    private readonly float[] _measImag;

    public int FftSize => _fftSize;

    public TransferFunctionCalculator(int fftSize, WindowType windowType = WindowType.Hann)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        _fftSize = fftSize;
        _fft = new FastFourierTransform(fftSize);
        _window = Windowing.Create(windowType, fftSize);

        _refReal = new float[fftSize];
        _refImag = new float[fftSize];
        _measReal = new float[fftSize];
        _measImag = new float[fftSize];
    }

    public void Calculate(ReadOnlySpan<float> referenceSignal, ReadOnlySpan<float> measurementSignal, TransferFunctionResult result)
    {
        if (referenceSignal.Length < _fftSize || measurementSignal.Length < _fftSize)
            throw new ArgumentException($"Input buffers must be at least of length {_fftSize}");

        if (result.FftSize != _fftSize)
            throw new ArgumentException("Result buffer FftSize does not match calculator FftSize");

        // 1. Aplicar Ventana
        Windowing.Apply(referenceSignal, _window, _refReal);
        Array.Clear(_refImag);

        Windowing.Apply(measurementSignal, _window, _measReal);
        Array.Clear(_measImag);

        // 2. Ejecutar FFTs
        _fft.Forward(_refReal, _refImag);
        _fft.Forward(_measReal, _measImag);

        // 3. Calcular función de transferencia H1, Magnitud (dB), Fase (deg) y Coherencia
        int binCount = result.BinCount;
        const float epsilon = 1e-12f;

        for (int k = 0; k < binCount; k++)
        {
            float xr = _refReal[k];
            float xi = _refImag[k];
            float yr = _measReal[k];
            float yi = _measImag[k];

            // Auto-espectros: Gxx = |X|^2, Gyy = |Y|^2
            float gxx = xr * xr + xi * xi;
            float gyy = yr * yr + yi * yi;

            // Espectro cruzado: Gxy = X* * Y = (xr - i*xi) * (yr + i*yi)
            float gxyReal = xr * yr + xi * yi;
            float gxyImag = xr * yi - xi * yr;

            if (gxx <= epsilon || gyy <= epsilon)
            {
                result.MagnitudeDb[k] = -120f;
                result.PhaseDegrees[k] = 0f;
                result.Coherence[k] = 0f;
            }
            else
            {
                // Estimador H1: H(f) = Gxy / Gxx
                float hReal = gxyReal / gxx;
                float hImag = gxyImag / gxx;

                // Magnitud (dB)
                float hMagSq = hReal * hReal + hImag * hImag;
                float hMag = MathF.Sqrt(hMagSq);
                result.MagnitudeDb[k] = 20f * MathF.Log10(Math.Max(hMag, 1e-6f));

                // Fase en grados [-180, +180]
                result.PhaseDegrees[k] = MathF.Atan2(hImag, hReal) * (180f / MathF.PI);

                // Coherencia: |Gxy|^2 / (Gxx * Gyy)
                float gxyMagSq = gxyReal * gxyReal + gxyImag * gxyImag;
                float coh = gxyMagSq / (gxx * gyy);
                result.Coherence[k] = Math.Clamp(coh, 0f, 1f);
            }
        }
    }
}
