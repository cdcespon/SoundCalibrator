using SoundCalibrator.Core.Calibration;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class MicrophoneCalibrationTests
{
    [Fact]
    public void LoadFromText_ParsesStandardCalibrationFilesWithComments()
    {
        string calText = @"# Sens 12.5 mV/Pa
* Dayton Audio EMM-6
20.0    -1.5   0.0
100.0   -0.2   0.0
1000.0   0.0   0.0
10000.0  1.8   0.0
20000.0  3.5   0.0
";
        var cal = new MicrophoneCalibration();
        cal.LoadFromText(calText);

        Assert.Equal(5, cal.Points.Count);
        Assert.Equal(20f, cal.Points[0].Frequency);
        Assert.Equal(-1.5f, cal.Points[0].MagnitudeDb);
        Assert.Equal(1000f, cal.Points[2].Frequency);
        Assert.Equal(0.0f, cal.Points[2].MagnitudeDb);
        Assert.Equal(20000f, cal.Points[4].Frequency);
        Assert.Equal(3.5f, cal.Points[4].MagnitudeDb);
    }

    [Fact]
    public void Interpolate_AtGeometricMidpoint_InterpolatesSmoothly()
    {
        string calText = @"
100.0   0.0   0.0
10000.0 2.0   0.0
";
        var cal = new MicrophoneCalibration();
        cal.LoadFromText(calText);

        // A 1000 Hz (exactamente el punto medio en escala logarítmica entre 100 y 10000 Hz)
        var (mag, _) = cal.Interpolate(1000f);

        Assert.InRange(mag, 0.99f, 1.01f); // Mitad de 2.0 dB = 1.0 dB
    }

    [Fact]
    public void ApplyCorrection_SubtractsMicrophoneDeviation()
    {
        string calText = @"
1000.0  2.5  10.0
";
        var cal = new MicrophoneCalibration();
        cal.LoadFromText(calText);

        float[] freqs = [1000f];
        float[] mag = [10.0f];
        float[] phase = [45.0f];

        cal.ApplyCorrection(freqs, mag, phase);

        Assert.InRange(mag[0], 7.49f, 7.51f); // 10.0 - 2.5 = 7.5 dB
        Assert.InRange(phase[0], 34.9f, 35.1f); // 45.0 - 10.0 = 35.0 deg
    }
}
