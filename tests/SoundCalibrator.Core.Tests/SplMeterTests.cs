using System;
using SoundCalibrator.Core.Calibration;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SplMeterTests
{
    [Fact]
    public void IEC61672_A_Weighting_ValuesMatchStandards()
    {
        // A-Weighting estándar:
        // 1000 Hz = 0.0 dB
        // 100 Hz = -19.1 dB (+/- 0.5 dB)
        // 10000 Hz = -2.5 dB (+/- 0.5 dB)
        Assert.Equal(0f, AcousticWeighting.GetAWeightingDb(1000f), 0.1f);
        Assert.InRange(AcousticWeighting.GetAWeightingDb(100f), -19.5f, -18.7f);
        Assert.InRange(AcousticWeighting.GetAWeightingDb(10000f), -3.0f, -2.0f);
    }

    [Fact]
    public void IEC61672_C_Weighting_ValuesMatchStandards()
    {
        // C-Weighting estándar:
        // 1000 Hz = 0.0 dB
        // 100 Hz = -0.3 dB (+/- 0.2 dB)
        // 10000 Hz = -4.4 dB (+/- 0.5 dB)
        Assert.Equal(0f, AcousticWeighting.GetCWeightingDb(1000f), 0.1f);
        Assert.InRange(AcousticWeighting.GetCWeightingDb(100f), -0.5f, -0.1f);
        Assert.InRange(AcousticWeighting.GetCWeightingDb(10000f), -4.8f, -4.0f);
    }

    [Fact]
    public void CalibrateWithTone_AdjustsOffsetAccurately()
    {
        var meter = new SplMeter();
        // Calibrador de 94.0 dB SPL genera lectura digital de -20.0 dBFS
        meter.CalibrateWithTone(measuredDbFsAt1kHz: -20.0f, calibratorLevelDbSpl: 94.0f);

        Assert.Equal(114.0f, meter.SplOffsetDb, 0.01f);

        // Si medimos una señal de -20 dBFS en 1kHz, debe reportar exactamente 94.0 dB SPL
        float[] freqs = [1000f];
        float[] mags = [-20f];

        var reading = meter.CalculateSpl(freqs, mags);
        Assert.Equal(94.0f, reading.DbZ, 0.1f);
        Assert.Equal(94.0f, reading.DbA, 0.1f);
        Assert.Equal(94.0f, reading.DbC, 0.1f);
    }
}
