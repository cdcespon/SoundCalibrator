using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class OctaveBandRtaCalculatorTests
{
    [Fact]
    public void CalculateBands_FullOctave_Calculates10Bands()
    {
        float[] freqs = [31.5f, 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f];
        float[] mags = new float[10];
        Array.Fill(mags, -20f);

        float[] destination = new float[10];
        int count = OctaveBandRtaCalculator.CalculateBands(freqs, mags, OctaveBandResolution.FullOctave, destination);

        Assert.Equal(10, count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(-20f, destination[i], precision: 1);
        }
    }

    [Fact]
    public void CalculateBands_ThirdOctave_Calculates31Bands()
    {
        float[] destination = new float[31];
        int count = OctaveBandRtaCalculator.CalculateBands(
            OctaveBandRtaCalculator.ThirdOctaveCenters,
            new float[31],
            OctaveBandResolution.ThirdOctave,
            destination);

        Assert.Equal(31, count);
    }

    [Fact]
    public void CalculateBands_PureTone1kHz_Dominates1kHzBand()
    {
        int n = 1024;
        float[] freqs = new float[n];
        float[] mags = new float[n];
        float df = 48000f / 2048f; // ~23.4 Hz per bin

        for (int i = 0; i < n; i++)
        {
            freqs[i] = i * df;
            mags[i] = -80f; // Piso de ruido
        }

        // Tono de 1 kHz a 0 dBFS
        int bin1k = (int)Math.Round(1000f / df);
        mags[bin1k] = 0.0f;

        float[] destination = new float[10];
        OctaveBandRtaCalculator.CalculateBands(freqs, mags, OctaveBandResolution.FullOctave, destination);

        // Banda 5 es 1000 Hz (31.5, 63, 125, 250, 500, 1000, 2000...)
        float level1k = destination[5];
        float level125 = destination[2];

        Assert.True(level1k >= -1.0f, $"1kHz band should be near 0 dBFS, was {level1k}");
        Assert.True(level125 < -40.0f, $"125Hz band should be low, was {level125}");
    }

    [Fact]
    public void CalculateBands_EmptyFrequencies_ReturnsZero()
    {
        float[] dest = new float[10];
        int res = OctaveBandRtaCalculator.CalculateBands(ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty, OctaveBandResolution.FullOctave, dest);
        Assert.Equal(0, res);
    }
}
