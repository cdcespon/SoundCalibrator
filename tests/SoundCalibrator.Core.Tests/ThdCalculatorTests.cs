using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class ThdCalculatorTests
{
    [Fact]
    public void PureFundamental_ReturnsNegligibleThd()
    {
        // 1000 bins: 0 to 20000 Hz, step 20 Hz
        int bins = 1000;
        float[] freqs = new float[bins];
        float[] mag = new float[bins];
        Array.Fill(mag, -90f); // Noise floor at -90 dB

        for (int i = 0; i < bins; i++) freqs[i] = i * 20f;

        // Fundamental at 1000 Hz (index 50) at 0 dB
        mag[50] = 0f;

        var result = ThdCalculator.Calculate(freqs, mag);

        Assert.Equal(1000f, result.FundamentalFreqHz, 10f);
        Assert.Equal(0f, result.FundamentalDb, 0.5f);
        Assert.True(result.ThdPercent < 0.1f, $"Pure fundamental THD should be < 0.1%, got {result.ThdPercent}%");
    }

    [Fact]
    public void KnownHarmonics_CalculatesAccurateThdPercent()
    {
        int bins = 1000;
        float[] freqs = new float[bins];
        float[] mag = new float[bins];
        Array.Fill(mag, -120f);

        for (int i = 0; i < bins; i++) freqs[i] = i * 20f;

        // Fundamental at 1000 Hz: 0 dB (voltage = 1.0)
        mag[50] = 0f;

        // 2nd harmonic at 2000 Hz: -20 dB (voltage = 0.1, i.e., 10% THD)
        mag[100] = -20f;

        var result = ThdCalculator.Calculate(freqs, mag, maxHarmonics: 2);

        // THD should be ~10% (0.1 / 1.0 * 100%)
        Assert.InRange(result.ThdPercent, 9.5f, 10.5f);
        Assert.InRange(result.ThdDb, -20.5f, -19.5f);
    }
}
