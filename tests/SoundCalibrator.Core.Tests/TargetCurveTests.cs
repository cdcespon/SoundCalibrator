using System;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class TargetCurveTests
{
    [Fact]
    public void FlatTargetCurve_ReturnsZeroDb_AcrossSpectrum()
    {
        var target = TargetCurve.CreatePreset(TargetCurvePreset.Flat);

        Assert.Equal(0f, target.Evaluate(20f), 0.01f);
        Assert.Equal(0f, target.Evaluate(1000f), 0.01f);
        Assert.Equal(0f, target.Evaluate(10000f), 0.01f);
    }

    [Fact]
    public void HarmanTargetCurve_HasBassBoostAndTrebleRollOff()
    {
        var target = TargetCurve.CreatePreset(TargetCurvePreset.HarmanTarget);

        float bassGain = target.Evaluate(40f);
        float midGain = target.Evaluate(1000f);
        float trebleGain = target.Evaluate(10000f);

        Assert.True(bassGain > midGain, "Harman curve must have boosted bass relative to 1kHz");
        Assert.True(trebleGain < midGain, "Harman curve must have rolled-off highs relative to 1kHz");
        Assert.Equal(0f, midGain, 0.1f);
    }

    [Fact]
    public void CinemaXCurve_FollowsIso2969RollOffAbove2kHz()
    {
        var target = TargetCurve.CreatePreset(TargetCurvePreset.CinemaXCurve);

        float gain1k = target.Evaluate(1000f);
        float gain2k = target.Evaluate(2000f);
        float gain4k = target.Evaluate(4000f);
        float gain8k = target.Evaluate(8000f);

        Assert.Equal(0f, gain1k, 0.1f);
        Assert.Equal(0f, gain2k, 0.1f);
        // ISO 2969 rolls off ~3 dB/octave above 2kHz
        Assert.InRange(gain4k, -3.5f, -2.5f);
        Assert.InRange(gain8k, -6.5f, -5.5f);
    }

    [Fact]
    public void EvaluateSpan_FillsBufferWithoutExceptions()
    {
        var target = TargetCurve.CreatePreset(TargetCurvePreset.BruelKjaer1974);
        float[] freqs = [50f, 1000f, 20000f];
        float[] gains = new float[3];

        target.Evaluate(freqs, gains);

        Assert.InRange(gains[0], 2.5f, 3.5f);
        Assert.Equal(0f, gains[1], 0.1f);
        Assert.InRange(gains[2], -3.5f, -2.5f);
    }

    [Fact]
    public void CustomPoints_InterpolatesAccurately()
    {
        var target = new TargetCurve("Custom", [
            new TargetCurvePoint(100f, 6f),
            new TargetCurvePoint(1000f, 0f)
        ]);

        // Geometric mean of 100 and 1000 is ~316.2 Hz, which should be halfway (+3 dB)
        float midFreq = MathF.Sqrt(100f * 1000f);
        float gainMid = target.Evaluate(midFreq);

        Assert.Equal(3.0f, gainMid, 0.1f);
    }
}
