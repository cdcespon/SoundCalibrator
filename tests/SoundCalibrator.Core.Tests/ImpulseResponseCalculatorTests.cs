using System;
using SoundCalibrator.Core.DSP;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class ImpulseResponseCalculatorTests
{
    [Fact]
    public void CalculateImpulseResponse_DetectsKnownDelayAccurately()
    {
        const int fftSize = 1024;
        const float sampleRate = 48000f;
        const int expectedDelaySamples = 120; // 120 samples = 2.5 ms
        float expectedDelayMs = (float)expectedDelaySamples * 1000f / sampleRate;

        var calculator = new ImpulseResponseCalculator(fftSize);
        int binCount = fftSize / 2 + 1;

        float[] magnitudeDb = new float[binCount]; // 0 dB plano
        float[] phaseDegrees = new float[binCount];
        float deltaF = sampleRate / fftSize;

        // Simular pendiente de fase lineal correspondiente a 120 muestras de retardo:
        // phase(f) = -360 * f * delay_seconds
        float delaySeconds = (float)expectedDelaySamples / sampleRate;
        for (int k = 0; k < binCount; k++)
        {
            float f = k * deltaF;
            float p = -360f * f * delaySeconds;
            // Envolver a [-180, +180]
            p = ((p + 180f) % 360f + 360f) % 360f - 180f;
            phaseDegrees[k] = p;
        }

        float[] ir = new float[fftSize];
        var delayResult = new DelayResult();

        calculator.CalculateImpulseResponse(magnitudeDb, phaseDegrees, ir, sampleRate, delayResult);

        Assert.Equal(expectedDelaySamples, delayResult.PeakIndex);
        Assert.InRange(delayResult.DelayMs, expectedDelayMs - 0.01f, expectedDelayMs + 0.01f);
        Assert.True(delayResult.DistanceMeters > 0.8f && delayResult.DistanceMeters < 0.9f); // ~0.857m
    }
}
