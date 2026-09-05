using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class MinimumPhaseAnalyzerTests
{
    [Fact]
    public void Analyze_EmptyInput_ReturnsEmptyResult()
    {
        var result = MinimumPhaseAnalyzer.Analyze([], [], []);
        Assert.Empty(result.Frequencies);
        Assert.Empty(result.MinPhaseDegrees);
        Assert.Empty(result.ExcessPhaseDegrees);
        Assert.Empty(result.ExcessGroupDelayMs);
    }

    [Fact]
    public void Analyze_FlatResponse_ZeroDelay_ProducesZeroPhaseAndDelay()
    {
        int count = 513; // FFT 1024 bins
        float df = 48000f / 1024f;
        float[] freqs = new float[count];
        float[] mag = new float[count];
        float[] phase = new float[count];

        for (int i = 0; i < count; i++)
        {
            freqs[i] = i * df;
            mag[i] = 0.0f; // 0 dB
            phase[i] = 0.0f;
        }

        var result = MinimumPhaseAnalyzer.Analyze(freqs, mag, phase);

        Assert.Equal(count, result.Frequencies.Length);
        Assert.Equal(count, result.MinPhaseDegrees.Length);
        Assert.Equal(count, result.ExcessPhaseDegrees.Length);
        Assert.Equal(count, result.ExcessGroupDelayMs.Length);

        // All should be ~0
        for (int i = 5; i < count - 5; i++)
        {
            Assert.Equal(0f, result.MinPhaseDegrees[i], 0.1f);
            Assert.Equal(0f, result.ExcessPhaseDegrees[i], 0.1f);
            Assert.Equal(0f, result.ExcessGroupDelayMs[i], 0.1f);
        }
    }

    [Fact]
    public void Analyze_PureDelay_IdentifiesExcessGroupDelayAccurately()
    {
        int count = 513; // FFT 1024 bins (48 kHz)
        float df = 48000f / 1024f;
        float delaySec = 0.002f; // 2.0 ms
        float[] freqs = new float[count];
        float[] mag = new float[count];
        float[] phase = new float[count];

        for (int i = 0; i < count; i++)
        {
            float f = i * df;
            freqs[i] = f;
            mag[i] = 0.0f; // flat 0 dB
            // Phase for delay tau: phi = -360 * f * tau
            float rawPhase = -360.0f * f * delaySec;
            phase[i] = ((rawPhase + 180f) % 360f + 360f) % 360f - 180f;
        }

        var result = MinimumPhaseAnalyzer.Analyze(freqs, mag, phase);

        // Since magnitude is flat, minimum phase must be ~0°
        // Excess group delay must recover the 2.0 ms acoustic flight time!
        for (int i = 10; i < 200; i++)
        {
            Assert.Equal(0.0f, result.MinPhaseDegrees[i], 0.2f);
            Assert.Equal(2.0f, result.ExcessGroupDelayMs[i], 0.15f);
        }
    }
}
