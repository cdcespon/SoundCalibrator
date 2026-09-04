using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.DSP;

public sealed class RtaCalculator
{
    private readonly int _fftSize;
    private readonly FastFourierTransform _fft;
    private readonly float[] _window;
    private readonly float[] _real;
    private readonly float[] _imag;
    private readonly float[] _maxHold;

    public int FftSize => _fftSize;
    public int BinCount => _fftSize / 2 + 1;

    public RtaCalculator(int fftSize, WindowType windowType = WindowType.Hann)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        _fftSize = fftSize;
        _fft = new FastFourierTransform(fftSize);
        _window = Windowing.Create(windowType, fftSize);

        _real = new float[fftSize];
        _imag = new float[fftSize];
        _maxHold = new float[BinCount];
        ResetMaxHold();
    }

    public void ResetMaxHold()
    {
        _maxHold.AsSpan().Fill(-140f);
    }

    public void Calculate(ReadOnlySpan<float> inputSignal, Span<float> rtaOutputDb, Span<float> maxHoldOutputDb)
    {
        if (inputSignal.Length < _fftSize)
            throw new ArgumentException($"Input signal length must be at least {_fftSize}");

        int count = Math.Min(BinCount, Math.Min(rtaOutputDb.Length, maxHoldOutputDb.Length));

        // 1. Aplicar ventana
        Windowing.Apply(inputSignal, _window, _real);
        Array.Clear(_imag);

        // 2. FFT directa
        _fft.Forward(_real, _imag);

        // 3. Normalización y dBFS (escala pico corregida por la ventana)
        // Para una onda senoidal de amplitud 1.0, el valor debe ser ~0 dBFS
        float normFactor = 2.0f / _fftSize;

        for (int k = 0; k < count; k++)
        {
            float r = _real[k] * normFactor;
            float i = _imag[k] * normFactor;

            float power = r * r + i * i;
            float mag = MathF.Sqrt(power);
            float db = 20f * MathF.Log10(Math.Max(mag, 1e-6f));

            rtaOutputDb[k] = db;

            // Actualizar Max Hold
            if (db > _maxHold[k])
            {
                _maxHold[k] = db;
            }

            maxHoldOutputDb[k] = _maxHold[k];
        }
    }
}
