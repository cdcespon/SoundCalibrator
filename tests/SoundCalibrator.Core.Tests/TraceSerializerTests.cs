using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Serialization;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class TraceSerializerTests
{
    [Fact]
    public void ExportAndImport_RoundtripsAccurately()
    {
        float[] freqs = [100f, 1000f, 10000f];
        float[] mag = [-1.5f, 0.0f, 2.5f];
        float[] phase = [-10.0f, 0.0f, 15.0f];
        float[] coh = [0.95f, 0.99f, 0.85f];

        var original = new AcousticTrace("Subwoofer + Main", "#FF4081", freqs, mag, phase, coh);

        string csv = TraceSerializer.ExportToCsv(original);
        var imported = TraceSerializer.ImportFromCsv(csv);

        Assert.Equal(original.Name, imported.Name);
        Assert.Equal(original.HexColor, imported.HexColor);
        Assert.Equal(3, imported.Frequencies.Length);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(original.Frequencies[i], imported.Frequencies[i]);
            Assert.InRange(imported.MagnitudeDb[i], original.MagnitudeDb[i] - 0.01f, original.MagnitudeDb[i] + 0.01f);
            Assert.InRange(imported.PhaseDegrees[i], original.PhaseDegrees[i] - 0.01f, original.PhaseDegrees[i] + 0.01f);
            Assert.InRange(imported.Coherence[i], original.Coherence[i] - 0.01f, original.Coherence[i] + 0.01f);
        }
    }
}
