using System;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Serialization;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class DspFilterExporterTests
{
    [Fact]
    public void ExportGenericCsv_OutputsValidHeaderAndFilterLines()
    {
        var filters = new PeqFilterSuggestion[]
        {
            new(100f, -3.5f, 2.0f, 0.71f),
            new(2500f, 2.0f, 4.5f, 0.32f)
        };

        string csv = DspFilterExporter.ExportGenericCsv(filters);

        Assert.Contains("Band,Type,FrequencyHz,GainDb,Q,BandwidthOctaves", csv);
        Assert.Contains("1,PEQ,100.0,-3.50,2.00,0.71", csv);
        Assert.Contains("2,PEQ,2500.0,+2.00,4.50,0.32", csv);
    }

    [Fact]
    public void ExportMiniDsp_OutputsValidCoefficientsFormat()
    {
        var filters = new PeqFilterSuggestion[]
        {
            new(1000f, -6.0f, 1.414f, 1.0f)
        };

        string miniDsp = DspFilterExporter.ExportMiniDsp(filters, 48000f);

        Assert.Contains("# miniDSP Biquad Coefficients", miniDsp);
        Assert.Contains("biquad1=", miniDsp);

        string[] lines = miniDsp.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var biquadLine = Assert.Single(lines, l => l.StartsWith("biquad1="));

        string coeffsStr = biquadLine["biquad1=".Length..];
        string[] coeffs = coeffsStr.Split(',');
        Assert.Equal(5, coeffs.Length); // b0, b1, b2, a1, a2
    }
}
