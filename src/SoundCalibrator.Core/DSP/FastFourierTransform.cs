using System;
using System.Numerics;

namespace SoundCalibrator.Core.DSP;

public sealed class FastFourierTransform
{
    private readonly int _length;
    private readonly int[] _bitReversedIndices;
    private readonly float[] _twiddleCos;
    private readonly float[] _twiddleSin;

    public int Length => _length;

    public FastFourierTransform(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if ((length & (length - 1)) != 0)
            throw new ArgumentException("Length must be a power of 2", nameof(length));

        _length = length;
        _bitReversedIndices = ComputeBitReversedIndices(length);

        int half = length / 2;
        _twiddleCos = new float[half];
        _twiddleSin = new float[half];

        for (int i = 0; i < half; i++)
        {
            float angle = -2.0f * MathF.PI * i / length;
            _twiddleCos[i] = MathF.Cos(angle);
            _twiddleSin[i] = MathF.Sin(angle);
        }
    }

    public void Forward(Span<float> real, Span<float> imag)
    {
        if (real.Length < _length || imag.Length < _length)
            throw new ArgumentException("Buffer length is smaller than FFT length");

        // 1. Bit-reversal permutation
        for (int i = 0; i < _length; i++)
        {
            int j = _bitReversedIndices[i];
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        // 2. Cooley-Tukey Radix-2 decimation-in-time
        for (int stageSize = 2; stageSize <= _length; stageSize <<= 1)
        {
            int halfStage = stageSize >> 1;
            int step = _length / stageSize;

            for (int k = 0; k < _length; k += stageSize)
            {
                for (int j = 0; j < halfStage; j++)
                {
                    int twiddleIdx = j * step;
                    float c = _twiddleCos[twiddleIdx];
                    float s = _twiddleSin[twiddleIdx];

                    int uIdx = k + j;
                    int vIdx = k + j + halfStage;

                    float vReal = real[vIdx];
                    float vImag = imag[vIdx];

                    // Multiplicación compleja con twiddle
                    float tReal = vReal * c - vImag * s;
                    float tImag = vReal * s + vImag * c;

                    float uReal = real[uIdx];
                    float uImag = imag[uIdx];

                    real[uIdx] = uReal + tReal;
                    imag[uIdx] = uImag + tImag;

                    real[vIdx] = uReal - tReal;
                    imag[vIdx] = uImag - tImag;
                }
            }
        }
    }

    private static int[] ComputeBitReversedIndices(int length)
    {
        var indices = new int[length];
        int bits = BitOperations.Log2((uint)length);

        for (int i = 0; i < length; i++)
        {
            int reversed = 0;
            for (int b = 0; b < bits; b++)
            {
                if ((i & (1 << b)) != 0)
                {
                    reversed |= 1 << (bits - 1 - b);
                }
            }
            indices[i] = reversed;
        }

        return indices;
    }
}
