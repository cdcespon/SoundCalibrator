using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class EtcCalculatorTests
{
    [Fact]
    public void Calculate_EmptyInput_ReturnsEmptyResult()
    {
        var result = EtcCalculator.Calculate([], 48000);
        Assert.Empty(result.TimeMs);
        Assert.Empty(result.EnvelopeDb);
        Assert.Empty(result.Reflections);
    }

    [Fact]
    public void Calculate_DirectSoundAndReflection_IdentifiesPeaksAndDistances()
    {
        int sampleRate = 48000;
        int length = 2048;
        float[] ir = new float[length];

        int directSample = 100;
        ir[directSample] = 1.0f;

        int reflectionSample = 340;
        ir[reflectionSample] = 0.5f;

        var etc = EtcCalculator.Calculate(ir, sampleRate, minDb: -80f, reflectionThresholdDb: -20f);

        Assert.Equal(length, etc.TimeMs.Length);
        Assert.Equal(length, etc.EnvelopeDb.Length);
        Assert.Equal(0.0f, etc.EnvelopeDb[directSample], 0.1f);

        // Single isolated reflection detected at 5.0ms relative
        var refl = Assert.Single(etc.Reflections);
        Assert.Equal(5.0f, refl.RelativeDelayMs, 0.1f);
        Assert.Equal(-6.02f, refl.LevelDb, 0.5f);
        Assert.Equal(1.715f, refl.PathDifferenceMeters, 0.05f);
    }

    [Fact]
    public void Calculate_MultipleDistinctReflections_DetectsAllMajorEchoes()
    {
        int sampleRate = 48000;
        int length = 4096;
        float[] ir = new float[length];

        // Direct sound at sample 200 (~4.17 ms)
        ir[200] = 1.0f;

        // Reflection 1 at sample 440 (+5 ms delay, 0.4 amplitude = -7.96 dB)
        ir[440] = 0.4f;

        // Reflection 2 at sample 680 (+10 ms delay, 0.25 amplitude = -12.04 dB)
        ir[680] = 0.25f;

        var etc = EtcCalculator.Calculate(ir, sampleRate, minDb: -80f, reflectionThresholdDb: -20f);

        Assert.Equal(2, etc.Reflections.Count);

        var r1 = etc.Reflections[0];
        Assert.Equal(5.0f, r1.RelativeDelayMs, 0.15f);
        Assert.Equal(-7.96f, r1.LevelDb, 0.5f);
        Assert.Equal(1.715f, r1.PathDifferenceMeters, 0.05f);

        var r2 = etc.Reflections[1];
        Assert.Equal(10.0f, r2.RelativeDelayMs, 0.15f);
        Assert.Equal(-12.04f, r2.LevelDb, 0.5f);
        Assert.Equal(3.43f, r2.PathDifferenceMeters, 0.05f);
    }
}
