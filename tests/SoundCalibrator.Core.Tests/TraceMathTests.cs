using System;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Operations;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class TraceMathTests
{
    [Fact]
    public void Subtract_ComputesDifferenceAccurately()
    {
        float[] a = [10f, -5f, 0f, 3.5f];
        float[] b = [2f, -5f, 4f, 1.5f];
        float[] result = new float[4];

        TraceMath.Subtract(a, b, result);

        Assert.Equal(8f, result[0], 0.001f);
        Assert.Equal(0f, result[1], 0.001f);
        Assert.Equal(-4f, result[2], 0.001f);
        Assert.Equal(2f, result[3], 0.001f);
    }

    [Fact]
    public void CalculateDelta_ComputesTargetErrorAndInvertedCorrection()
    {
        var target = TargetCurve.CreatePreset(TargetCurvePreset.HarmanTarget);
        float[] freqs = [40f, 1000f];
        float[] measured = [7f, -2f];

        float[] delta = new float[2];
        float[] correction = new float[2];

        TraceMath.CalculateDelta(freqs, measured, target, delta, correction);

        Assert.Equal(2f, delta[0], 0.01f);
        Assert.Equal(-2f, delta[1], 0.01f);

        Assert.Equal(-2f, correction[0], 0.01f);
        Assert.Equal(2f, correction[1], 0.01f);
    }

    [Fact]
    public void DivideTraces_ComputesRelativeTransferFunctionAccurately()
    {
        float[] freqs = [100f, 1000f, 5000f];
        float[] magA = [10f, 5f, -3f];
        float[] phaseA = [90f, -45f, 170f];
        float[] cohA = [0.9f, 0.8f, 0.5f];
        var traceA = new AcousticTrace("Trace A", "#FF0000", freqs, magA, phaseA, cohA);

        float[] magB = [4f, 10f, -3f];
        float[] phaseB = [30f, 45f, -170f];
        float[] cohB = [0.8f, 0.5f, 0.4f];
        var traceB = new AcousticTrace("Trace B", "#00FF00", freqs, magB, phaseB, cohB);

        var diff = TraceMath.DivideTraces(traceA, traceB, "Diff (A/B)", "#E040FB");

        Assert.NotNull(diff);
        Assert.Equal(3, diff.Frequencies.Length);

        // Mag in dB: MagA - MagB
        Assert.Equal(6f, diff.MagnitudeDb[0], 0.01f);
        Assert.Equal(-5f, diff.MagnitudeDb[1], 0.01f);
        Assert.Equal(0f, diff.MagnitudeDb[2], 0.01f);

        // Phase wrapped [-180, 180]:
        // 90 - 30 = 60
        Assert.Equal(60f, diff.PhaseDegrees[0], 0.01f);
        // -45 - 45 = -90
        Assert.Equal(-90f, diff.PhaseDegrees[1], 0.01f);
        // 170 - (-170) = 340 => wrapped: -20 deg
        Assert.Equal(-20f, diff.PhaseDegrees[2], 0.01f);

        // Coherence: cohA * cohB
        Assert.Equal(0.72f, diff.Coherence[0], 0.01f);
        Assert.Equal(0.40f, diff.Coherence[1], 0.01f);
        Assert.Equal(0.20f, diff.Coherence[2], 0.01f);
    }

    [Fact]
    public void DivideTraces_ThrowsOnMismatchedFrequencies()
    {
        var traceA = new AcousticTrace("A", "#FF0000", [100f, 1000f], [0f, 0f], [0f, 0f], [1f, 1f]);
        var traceB = new AcousticTrace("B", "#00FF00", [100f], [0f], [0f], [1f]);

        Assert.Throws<ArgumentException>(() => TraceMath.DivideTraces(traceA, traceB));
    }
}
