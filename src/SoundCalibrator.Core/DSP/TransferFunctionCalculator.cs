using System;
using SoundCalibrator.Core.Averaging;
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

    // Espectros instantáneos
    private readonly float[] _gxx;
    private readonly float[] _gyy;
    private readonly float[] _gxyReal;
    private readonly float[] _gxyImag;

    public int FftSize => _fftSize;
    public int BinCount => _fftSize / 2 + 1;

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

        int binCount = BinCount;
        _gxx = new float[binCount];
        _gyy = new float[binCount];
        _gxyReal = new float[binCount];
        _gxyImag = new float[binCount];
    }

    public void Calculate(ReadOnlySpan<float> referenceSignal, ReadOnlySpan<float> measurementSignal, TransferFunctionResult result)
    {
        ComputeSpectra(referenceSignal, measurementSignal);
        ComputeResultDirect(result);
    }

    public void Calculate(ReadOnlySpan<float> referenceSignal, ReadOnlySpan<float> measurementSignal, SpectralAverager averager, TransferFunctionResult result)
    {
        ComputeSpectra(referenceSignal, measurementSignal);
        averager.Process(_gxx, _gyy, _gxyReal, _gxyImag, result);
    }

    private void ComputeSpectra(ReadOnlySpan<float> referenceSignal, ReadOnlySpan<float> measurementSignal)
    {
        if (referenceSignal.Length < _fftSize || measurementSignal.Length < _fftSize)
            throw new ArgumentException($"Input buffers must be at least of length {_fftSize}");

        // 1. Ventana
        Windowing.Apply(referenceSignal, _window, _refReal);
        Array.Clear(_refImag);

        Windowing.Apply(measurementSignal, _window, _measReal);
        Array.Clear(_measImag);

        // 2. FFTs
        _fft.Forward(_refReal, _refImag);
        _fft.Forward(_measReal, _measImag);

        // 3. Auto y cross spectra
        int binCount = BinCount;
        for (int k = 0; k < binCount; k++)
        {
            float xr = _refReal[k];
            float xi = _refImag[k];
            float yr = _measReal[k];
            float yi = _measImag[k];

            _gxx[k] = xr * xr + xi * xi;
            _gyy[k] = yr * yr + yi * yi;
            _gxyReal[k] = xr * yr + xi * yi;
            _gxyImag[k] = xr * yi - xi * yr;
        }
    }

    private void ComputeResultDirect(TransferFunctionResult result)
    {
        if (result.FftSize != _fftSize)
            throw new ArgumentException("Result buffer FftSize does not match calculator FftSize");

        int binCount = BinCount;
        const float epsilon = 1e-12f;

        for (int k = 0; k < binCount; k++)
        {
            float gxx = _gxx[k];
            float gyy = _gyy[k];
            float gxyReal = _gxyReal[k];
            float gxyImag = _gxyImag[k];

            if (gxx <= epsilon || gyy <= epsilon)
            {
                result.MagnitudeDb[k] = -120f;
                result.PhaseDegrees[k] = 0f;
                result.Coherence[k] = 0f;
            }
            else
            {
                float hReal = gxyReal / gxx;
                float hImag = gxyImag / gxx;

                float hMagSq = hReal * hReal + hImag * hImag;
                result.MagnitudeDb[k] = 20f * MathF.Log10(Math.Max(MathF.Sqrt(hMagSq), 1e-6f));
                result.PhaseDegrees[k] = MathF.Atan2(hImag, hReal) * (180f / MathF.PI);

                float gxyMagSq = gxyReal * gxyReal + gxyImag * gxyImag;
                float coh = gxyMagSq / (gxx * gyy);
                result.Coherence[k] = Math.Clamp(coh, 0f, 1f);
            }
        }
    }
}
