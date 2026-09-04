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
}
