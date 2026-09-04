using System;

namespace SoundCalibrator.Core.Models;

public sealed class TransferFunctionResult
{
    public int FftSize { get; }
    public int BinCount => FftSize / 2 + 1;

    public float[] MagnitudeDb { get; }
    public float[] PhaseDegrees { get; }
    public float[] Coherence { get; }

    public TransferFunctionResult(int fftSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        if ((fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FftSize must be a power of 2", nameof(fftSize));

        FftSize = fftSize;
        int count = BinCount;
        MagnitudeDb = new float[count];
        PhaseDegrees = new float[count];
        Coherence = new float[count];
    }

    public void Clear()
    {
        Array.Clear(MagnitudeDb);
        Array.Clear(PhaseDegrees);
        Array.Clear(Coherence);
    }
}
