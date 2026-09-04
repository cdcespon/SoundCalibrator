using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Averaging;

public sealed class SpectralAverager
{
    private readonly int _binCount;
    private readonly float[] _avgGxx;
    private readonly float[] _avgGyy;
    private readonly float[] _avgGxyReal;
    private readonly float[] _avgGxyImag;

    private int _sampleCount;
    public AveragingType Mode { get; set; } = AveragingType.ExponentialFast;
    public int SampleCount => _sampleCount;

    public SpectralAverager(int fftSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        _binCount = fftSize / 2 + 1;
        _avgGxx = new float[_binCount];
        _avgGyy = new float[_binCount];
        _avgGxyReal = new float[_binCount];
        _avgGxyImag = new float[_binCount];
    }

    public void Reset()
    {
        _sampleCount = 0;
        Array.Clear(_avgGxx);
        Array.Clear(_avgGyy);
        Array.Clear(_avgGxyReal);
        Array.Clear(_avgGxyImag);
    }

    public void Process(
        ReadOnlySpan<float> gxx,
        ReadOnlySpan<float> gyy,
        ReadOnlySpan<float> gxyReal,
        ReadOnlySpan<float> gxyImag,
        TransferFunctionResult outputResult)
    {
        int count = Math.Min(_binCount, outputResult.BinCount);
        _sampleCount++;

        float alpha = GetAlpha(_sampleCount);

        for (int k = 0; k < count; k++)
        {
            if (_sampleCount == 1 || Mode == AveragingType.None)
            {
                _avgGxx[k] = gxx[k];
                _avgGyy[k] = gyy[k];
                _avgGxyReal[k] = gxyReal[k];
                _avgGxyImag[k] = gxyImag[k];
            }
            else
            {
                // Promedio ponderado exponencial: avg = (1 - alpha) * avg + alpha * current
                _avgGxx[k] = (1f - alpha) * _avgGxx[k] + alpha * gxx[k];
                _avgGyy[k] = (1f - alpha) * _avgGyy[k] + alpha * gyy[k];
                _avgGxyReal[k] = (1f - alpha) * _avgGxyReal[k] + alpha * gxyReal[k];
                _avgGxyImag[k] = (1f - alpha) * _avgGxyImag[k] + alpha * gxyImag[k];
            }

            float avgGxxVal = _avgGxx[k];
            float avgGyyVal = _avgGyy[k];
            float avgGxyR = _avgGxyReal[k];
            float avgGxyI = _avgGxyImag[k];

            const float epsilon = 1e-12f;
            if (avgGxxVal <= epsilon || avgGyyVal <= epsilon)
            {
                outputResult.MagnitudeDb[k] = -120f;
                outputResult.PhaseDegrees[k] = 0f;
                outputResult.Coherence[k] = 0f;
            }
            else
            {
                // Estimador H1 con espectros promediados
                float hReal = avgGxyR / avgGxxVal;
                float hImag = avgGxyI / avgGxxVal;

                float hMagSq = hReal * hReal + hImag * hImag;
                outputResult.MagnitudeDb[k] = 20f * MathF.Log10(Math.Max(MathF.Sqrt(hMagSq), 1e-6f));
                outputResult.PhaseDegrees[k] = MathF.Atan2(hImag, hReal) * (180f / MathF.PI);

                // Coherencia promediada
                float gxyMagSq = avgGxyR * avgGxyR + avgGxyI * avgGxyI;
                float coh = gxyMagSq / (avgGxxVal * avgGyyVal);
                outputResult.Coherence[k] = Math.Clamp(coh, 0f, 1f);
            }
        }
    }

    private float GetAlpha(int currentCount) => Mode switch
    {
        AveragingType.None => 1.0f,
        AveragingType.ExponentialFast => 0.25f, // Rápido (~4 frames)
        AveragingType.ExponentialSlow => 0.05f, // Lento (~20 frames)
        AveragingType.Linear16 => 1.0f / Math.Min(currentCount, 16),
        AveragingType.Linear64 => 1.0f / Math.Min(currentCount, 64),
        AveragingType.Infinite => 1.0f / currentCount,
        _ => 0.2f
    };
}
