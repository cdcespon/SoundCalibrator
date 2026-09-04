using System;
using SoundCalibrator.Core.DSP;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class FastFourierTransformTests
{
    [Fact]
    public void ForwardAndInverse_ReconstructsOriginalSignalAccurately()
    {
        const int length = 512;
        var fft = new FastFourierTransform(length);

        float[] original = new float[length];
        float[] real = new float[length];
        float[] imag = new float[length];

        for (int i = 0; i < length; i++)
        {
            original[i] = MathF.Sin(2f * MathF.PI * 440f * i / 48000f) + 0.5f * MathF.Cos(2f * MathF.PI * 1200f * i / 48000f);
            real[i] = original[i];
        }

        // Directa
        fft.Forward(real, imag);

        // Inversa
        fft.Inverse(real, imag);

        for (int i = 0; i < length; i++)
        {
            Assert.InRange(real[i], original[i] - 1e-4f, original[i] + 1e-4f);
            Assert.InRange(imag[i], -1e-4f, 1e-4f); // Parte imaginaria debe ser ~0
        }
    }
}
