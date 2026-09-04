using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class GroupDelayCalculatorTests
{
    [Fact]
    public void CalculateGroupDelayMs_PureDelay5ms_ReturnsExact5ms()
    {
        // 5 ms de retardo puro
        const float DelayMs = 5.0f;
        int n = 100;
        float[] freqs = new float[n];
        float[] phaseDeg = new float[n];
        float[] groupDelay = new float[n];

        for (int i = 0; i < n; i++)
        {
            freqs[i] = 100f + i * 50f; // 100 Hz a 5050 Hz
            // Fase = -360 * f * (DelayMs / 1000)
            phaseDeg[i] = -360f * freqs[i] * (DelayMs / 1000f);
        }

        GroupDelayCalculator.CalculateGroupDelayMs(freqs, phaseDeg, groupDelay);

        // Los puntos interiores deben dar exactamente 5.0 ms
        for (int i = 1; i < n - 1; i++)
        {
            Assert.Equal(DelayMs, groupDelay[i], precision: 2);
        }
    }

    [Fact]
    public void CalculateGroupDelayMs_ZeroPhase_Returns0ms()
    {
        float[] freqs = [100f, 200f, 300f, 400f];
        float[] phaseDeg = [0f, 0f, 0f, 0f];
        float[] groupDelay = new float[4];

        GroupDelayCalculator.CalculateGroupDelayMs(freqs, phaseDeg, groupDelay);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0f, groupDelay[i], precision: 4);
        }
    }

    [Fact]
    public void CalculatePhaseDelayMs_PureDelay5ms_Returns5ms()
    {
        const float DelayMs = 5.0f;
        float[] freqs = [100f, 500f, 1000f, 2000f];
        float[] phaseDeg = new float[freqs.Length];
        float[] phaseDelay = new float[freqs.Length];

        for (int i = 0; i < freqs.Length; i++)
        {
            phaseDeg[i] = -360f * freqs[i] * (DelayMs / 1000f);
        }

        GroupDelayCalculator.CalculatePhaseDelayMs(freqs, phaseDeg, phaseDelay);

        for (int i = 0; i < freqs.Length; i++)
        {
            Assert.Equal(DelayMs, phaseDelay[i], precision: 2);
        }
    }
}
