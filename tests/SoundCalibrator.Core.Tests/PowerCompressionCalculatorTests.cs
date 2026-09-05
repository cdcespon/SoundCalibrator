using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class PowerCompressionCalculatorTests
{
    [Fact]
    public void Calculate_EmptyInput_ReturnsEmptyResult()
    {
        var result = PowerCompressionCalculator.Calculate([], [], [], 10f);
        Assert.Empty(result.Frequencies);
        Assert.Empty(result.CompressionDb);
        Assert.Equal(0f, result.BroadbandCompressionDb);
    }

    [Fact]
    public void Calculate_LinearSpeaker_ReportsZeroCompression()
    {
        float[] freqs = [50f, 100f, 1000f, 10000f];
        float[] baseline = [70f, 85f, 90f, 88f];
        // Drive elevated by +10 dB, perfectly linear response:
        float[] highDrive = [80f, 95f, 100f, 98f];

        var result = PowerCompressionCalculator.Calculate(freqs, baseline, highDrive, driveLevelDeltaDb: 10f);

        Assert.Equal(4, result.CompressionDb.Length);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0f, result.CompressionDb[i], 0.01f);
        }
        Assert.Equal(0f, result.BroadbandCompressionDb, 0.01f);
        Assert.Equal(0f, result.MaxCompressionDb, 0.01f);
    }

    [Fact]
    public void Calculate_CompressedWoofer_IdentifiesLossAndWorstFrequency()
    {
        float[] freqs = [40f, 80f, 1000f];
        float[] baseline = [70f, 80f, 90f];
        // Drive elevated by +12 dB:
        // Expected: 82dB, 92dB, 102dB
        // Actual: 79dB (3dB compression at 40Hz), 90.5dB (1.5dB at 80Hz), 101.5dB (0.5dB at 1kHz)
        float[] highDrive = [79f, 90.5f, 101.5f];

        var result = PowerCompressionCalculator.Calculate(freqs, baseline, highDrive, driveLevelDeltaDb: 12f);

        Assert.Equal(3.0f, result.CompressionDb[0], 0.01f);
        Assert.Equal(1.5f, result.CompressionDb[1], 0.01f);
        Assert.Equal(0.5f, result.CompressionDb[2], 0.01f);

        Assert.Equal(3.0f, result.MaxCompressionDb, 0.01f);
        Assert.Equal(40.0f, result.WorstFrequencyHz, 0.01f);
        Assert.Equal((3f + 1.5f + 0.5f) / 3f, result.BroadbandCompressionDb, 0.01f);
    }
}
