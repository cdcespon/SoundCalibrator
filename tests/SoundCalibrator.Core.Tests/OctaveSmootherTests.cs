using System;
using SoundCalibrator.Core.Smoothing;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class OctaveSmootherTests
{
    [Fact]
    public void Smooth_None_LeavesInputUnchanged()
    {
        float[] input = [10f, 20f, 30f, 40f, 50f];
        float[] output = new float[input.Length];

        OctaveSmoother.Smooth(input, output, OctaveSmoothingType.None, 48000f, 1024);

        Assert.Equal(input, output);
    }

    [Fact]
    public void Smooth_Octave1_3_ReducesVarianceOnNoisySpectrum()
    {
        const int count = 513; // 1024 FFT
        float[] noisy = new float[count];
        float[] smoothed = new float[count];

        var rng = new Random(123);
        for (int i = 0; i < count; i++)
        {
            // Curva plana de 0 dB con ruido de +/- 6 dB
            noisy[i] = (float)(rng.NextDouble() * 12.0 - 6.0);
        }

        OctaveSmoother.Smooth(noisy, smoothed, OctaveSmoothingType.Octave1_3, 48000f, 1024);

        // La varianza de los bins superiores (por ej. entre 500 Hz y 5000 Hz, bins 20 a 100)
        // debe reducirse drásticamente al promediar en 1/3 de octava
        float varNoisy = CalculateVariance(noisy.AsSpan(20, 80));
        float varSmoothed = CalculateVariance(smoothed.AsSpan(20, 80));

        Assert.True(varSmoothed < varNoisy * 0.4f, $"Expected variance to drop by >60%, was {varNoisy} vs {varSmoothed}");
    }

    private static float CalculateVariance(ReadOnlySpan<float> data)
    {
        float mean = 0f;
        foreach (float v in data) mean += v;
        mean /= data.Length;

        float sumSq = 0f;
        foreach (float v in data)
        {
            float diff = v - mean;
            sumSq += diff * diff;
        }
        return sumSq / data.Length;
    }
}
