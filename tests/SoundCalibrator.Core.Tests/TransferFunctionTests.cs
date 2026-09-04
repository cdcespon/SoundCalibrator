using System;
using SoundCalibrator.Core.DSP;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class TransferFunctionTests
{
    private const int FftSize = 1024;
    private const float SampleRate = 48000f;

    [Fact]
    public void IdentitySignal_YieldsZeroDbZeroPhaseAndUnityCoherence()
    {
        var calculator = new TransferFunctionCalculator(FftSize, WindowType.Hann);
        var refSignal = GenerateSineWave(1000f, 1.0f, 0f, SampleRate, FftSize);
        var measSignal = (float[])refSignal.Clone();

        var result = new TransferFunctionResult(FftSize);

        calculator.Calculate(refSignal, measSignal, result);

        int bin1k = (int)MathF.Round(1000f / (SampleRate / FftSize));

        Assert.InRange(result.MagnitudeDb[bin1k], -0.1f, 0.1f);
        Assert.InRange(result.PhaseDegrees[bin1k], -1.0f, 1.0f);
        Assert.InRange(result.Coherence[bin1k], 0.99f, 1.01f);
    }

    [Fact]
    public void GainAndPhaseShift_CalculatesAccurately()
    {
        var calculator = new TransferFunctionCalculator(FftSize, WindowType.Hann);
        float testFreq = 2000f;
        
        var refSignal = GenerateSineWave(testFreq, 0.5f, 0f, SampleRate, FftSize);
        var measSignal = GenerateSineWave(testFreq, 1.0f, -MathF.PI / 2f, SampleRate, FftSize);

        var result = new TransferFunctionResult(FftSize);

        calculator.Calculate(refSignal, measSignal, result);

        int bin2k = (int)MathF.Round(testFreq / (SampleRate / FftSize));

        Assert.InRange(result.MagnitudeDb[bin2k], 5.9f, 6.15f);
        Assert.InRange(result.PhaseDegrees[bin2k], -92.0f, -88.0f);
        Assert.InRange(result.Coherence[bin2k], 0.98f, 1.01f);
    }

    [Fact]
    public void SilenceInput_DoesNotThrowAndYieldsZeroCoherence()
    {
        var calculator = new TransferFunctionCalculator(FftSize, WindowType.Hann);
        var refSignal = new float[FftSize];
        var measSignal = new float[FftSize];
        var result = new TransferFunctionResult(FftSize);

        var ex = Record.Exception(() => calculator.Calculate(refSignal, measSignal, result));
        Assert.Null(ex);

        for (int i = 0; i < result.BinCount; i++)
        {
            Assert.False(float.IsNaN(result.MagnitudeDb[i]));
            Assert.False(float.IsInfinity(result.MagnitudeDb[i]));
            Assert.Equal(0f, result.Coherence[i]);
        }
    }

    [Fact]
    public void Calculate_HasZeroHeapAllocationsInHotLoop()
    {
        var calculator = new TransferFunctionCalculator(FftSize, WindowType.Hann);
        var refSignal = new float[FftSize];
        var measSignal = new float[FftSize];
        var result = new TransferFunctionResult(FftSize);

        // Warm up JIT
        calculator.Calculate(refSignal, measSignal, result);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            calculator.Calculate(refSignal, measSignal, result);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private static float[] GenerateSineWave(float frequency, float amplitude, float phaseOffsetRad, float sampleRate, int samples)
    {
        var buffer = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            buffer[i] = amplitude * MathF.Sin(2f * MathF.PI * frequency * t + phaseOffsetRad);
        }
        return buffer;
    }
}
