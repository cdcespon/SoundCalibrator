using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class ImdCalculatorTests
{
    [Fact]
    public void CalculateSmpte_Empty_ReturnsEmptyResult()
    {
        var result = ImdCalculator.CalculateSmpte([], []);
        Assert.Equal(ImdStandard.Smpte, result.Standard);
        Assert.Equal(0f, result.ImdPercent);
        Assert.Empty(result.Products);
    }

    [Fact]
    public void CalculateSmpte_SimulatedSidebands_ComputesAccurateImd()
    {
        int count = 2048; // Resolution ~23.4 Hz
        float df = 48000f / 2048f;
        float[] freqs = new float[count];
        float[] spectrum = new float[count];

        for (int i = 0; i < count; i++)
        {
            freqs[i] = i * df;
            spectrum[i] = -100f; // Noise floor
        }

        // Carrier at 7000 Hz: -6 dBFS
        int carrierBin = (int)MathF.Round(7000f / df);
        spectrum[carrierBin] = -6.0f;

        // Sidebands at 7000 +/- 60 Hz (6940 Hz and 7060 Hz) at -46 dBFS (-40 dB relative = 1%)
        int sbMinus = (int)MathF.Round(6940f / df);
        int sbPlus = (int)MathF.Round(7060f / df);
        spectrum[sbMinus] = -46.0f;
        spectrum[sbPlus] = -46.0f;

        var result = ImdCalculator.CalculateSmpte(freqs, spectrum, f1: 60f, f2: 7000f, toleranceHz: 30f);

        Assert.Equal(ImdStandard.Smpte, result.Standard);
        Assert.Equal(4, result.Products.Count);

        // Carrier at -6 dB
        Assert.Equal(-6.0f, result.PrimaryToneDb, 0.5f);

        // Total IMD should be sqrt(1%^2 + 1%^2) = 1.414%
        Assert.Equal(1.41f, result.ImdPercent, 0.15f);
        Assert.Equal(-37.0f, result.ImdDb, 1.0f);
    }

    [Fact]
    public void CalculateCcif_SimulatedDifferenceProduct_CalculatesDfdAccurately()
    {
        int count = 2048;
        float df = 48000f / 2048f;
        float[] freqs = new float[count];
        float[] spectrum = new float[count];

        for (int i = 0; i < count; i++)
        {
            freqs[i] = i * df;
            spectrum[i] = -100f;
        }

        // Tones at 19 kHz and 20 kHz at -6 dBFS
        int bin19 = (int)MathF.Round(19000f / df);
        int bin20 = (int)MathF.Round(20000f / df);
        spectrum[bin19] = -6.0f;
        spectrum[bin20] = -6.0f;

        // Difference frequency product at 1 kHz (20k - 19k) at -46 dBFS (-40 dB = 1%)
        int bin1k = (int)MathF.Round(1000f / df);
        spectrum[bin1k] = -46.0f;

        var result = ImdCalculator.CalculateCcif(freqs, spectrum, f1: 19000f, f2: 20000f, toleranceHz: 30f);

        Assert.Equal(ImdStandard.Ccif, result.Standard);
        Assert.Equal(3, result.Products.Count);

        // Difference frequency d2 product at 1000 Hz
        Assert.Equal(-46.0f, result.Products[0].LevelDb, 0.5f);

        // Total primary power = sqrt(V^2 + V^2) = V * sqrt(2)
        // Ratio = V_1k / (V * sqrt(2)) = 0.01 / 1.414 = 0.707%
        Assert.Equal(0.71f, result.ImdPercent, 0.1f);
    }
}
