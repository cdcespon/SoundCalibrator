using System;
using System.Text;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Reporting;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class PdfReportGeneratorTests
{
    [Fact]
    public void GeneratePdf_ProducesValidPdfDocumentStructure()
    {
        var data = new CalibrationReportData
        {
            ProjectName = "Acoustic Stage Calibration",
            EngineerName = "Lead Audio Tech",
            Timestamp = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc),
            SampleRate = 48000,
            FftSize = 2048,
            WindowType = "Hann",
            AveragingType = "Fast",
            TargetCurveName = "Harman",
            DelayCompensationMs = 4.2f,
            InvertPolarity = false,
            Rt60 = new ReverberationTimeResult(true, 0.40f, 0.45f, 0.48f, 15f, 58f),
            Sti = new StiResult(0.82f, 2.1f, "Excellent"),
            Thd = new ThdResult(1000f, -3.0f, 0.12f, -58.4f, 0.18f, -54.9f, [-60f, -65f]),
            Imd = new ImdResult
            {
                Standard = ImdStandard.Smpte,
                PrimaryToneDb = -6.0f,
                ImdPercent = 0.22f,
                ImdDb = -53.2f,
                Products = [new("2nd Order (7k-60)", 6940f, -56f)]
            },
            Alignment = new AlignmentSuggestion(80f, -40f, 50f, 90f, 3.12f, 1.07f, false, 2.95f),
            PeqFilters = [
                new(62.5f, -4.2f, 3.4f, 0.42f),
                new(145.0f, 2.8f, 2.1f, 0.67f)
            ],
            DelayMatrix = new DelayMatrixReport("Main Left", 20.0f, 343.4f, [
                new("Sub Center", 8.58f, 4.40f, 1.51f, 4.95f)
            ]),
            Traces = [
                new AcousticTrace("Main L", "#00F0FF", [1000f], [0f], [0f], [0.98f]),
                new AcousticTrace("Sub C", "#10B981", [80f], [2f], [-45f], [0.95f])
            ]
        };

        byte[] pdf = ReportGenerator.GeneratePdf(data);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 500, "PDF should contain valid content and structure");

        string asciiHeader = Encoding.ASCII.GetString(pdf[..8]);
        Assert.StartsWith("%PDF-1.4", asciiHeader);

        string fullPdf = Encoding.ASCII.GetString(pdf);
        Assert.Contains("/Type /Catalog", fullPdf);
        Assert.Contains("/Type /Pages", fullPdf);
        Assert.Contains("/Type /Page", fullPdf);
        Assert.Contains("xref", fullPdf);
        Assert.Contains("trailer", fullPdf);
        Assert.Contains("startxref", fullPdf);
        Assert.Contains("%%EOF", fullPdf);
    }

    [Fact]
    public void GeneratePdf_HandlesEmptyOrMinimalReportGracefully()
    {
        var data = new CalibrationReportData
        {
            ProjectName = "Minimal Session",
            EngineerName = "Engineer",
            SampleRate = 44100,
            FftSize = 1024
        };

        byte[] pdf = ReportGenerator.GeneratePdf(data);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 200);
        string fullPdf = Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", fullPdf);
        Assert.Contains("%%EOF", fullPdf);
    }
}
