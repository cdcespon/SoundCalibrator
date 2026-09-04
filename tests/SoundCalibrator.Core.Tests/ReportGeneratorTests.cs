using System;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Reporting;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class ReportGeneratorTests
{
    [Fact]
    public void GenerateMarkdown_ContainsAllKeyAcousticMetrics()
    {
        var data = new CalibrationReportData
        {
            ProjectName = "Main Auditorium Tuning",
            EngineerName = "Carlos Cespon",
            Timestamp = new DateTime(2026, 9, 4, 15, 0, 0, DateTimeKind.Utc),
            SampleRate = 48000,
            FftSize = 1024,
            WindowType = "Hann",
            AveragingType = "ExponentialFast",
            TargetCurveName = "Harman Target",
            DelayCompensationMs = 2.5f,
            InvertPolarity = false,
            Rt60 = new ReverberationTimeResult(true, 0.42f, 0.45f, 0.46f, 12.5f, 55f),
            Thd = new ThdResult(1000f, -6.0f, 0.08f, -62.0f, 0.15f, -56.5f, [-65f, -72f]),
            Alignment = new AlignmentSuggestion(80f, -45f, 45f, 90f, 3.12f, 1.07f, false, 3.01f),
            Traces = [
                new AcousticTrace("Subwoofer", "#FF0000", [80f], [0f], [0f], [1f]),
                new AcousticTrace("Main PA", "#00FF00", [80f], [0f], [90f], [1f])
            ]
        };

        string md = ReportGenerator.GenerateMarkdown(data);

        Assert.Contains("# Acoustic Calibration Report", md);
        Assert.Contains("Main Auditorium Tuning", md);
        Assert.Contains("Carlos Cespon", md);
        Assert.Contains("Harman Target", md);
        Assert.Contains("0.45s", md); // RT60 T20
        Assert.Contains("0.08%", md); // THD
        Assert.Contains("3.12 ms", md); // Crossover delay
        Assert.Contains("Subwoofer", md);
        Assert.Contains("Main PA", md);
    }

    [Fact]
    public void GenerateHtml_ProducesValidSelfContainedDocument()
    {
        var data = new CalibrationReportData
        {
            ProjectName = "Studio Control Room",
            EngineerName = "Acoustic Tech",
            Timestamp = DateTime.UtcNow,
            SampleRate = 48000,
            FftSize = 2048,
            WindowType = "BlackmanHarris",
            AveragingType = "Linear16",
            TargetCurveName = "B&K 1974",
            Traces = []
        };

        string html = ReportGenerator.GenerateHtml(data);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("Studio Control Room", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void GenerateMarkdown_WithStiAndDelayMatrix_RendersNewSections()
    {
        var alignments = new AcousticZoneAlignment[]
        {
            new("Main PA", 50.0f, 0.0f, 0.0f, 0.0f),
            new("Delay Tower", 18.0f, 32.0f, 10.98f, 36.0f)
        };
        var delayMatrix = new DelayMatrixReport("Main PA", 20.0f, 343.2f, alignments);

        var data = new CalibrationReportData
        {
            ProjectName = "Stadium Sound Tuning",
            EngineerName = "Carlos Cespon",
            Timestamp = DateTime.UtcNow,
            Rt60 = new ReverberationTimeResult(true, 1.2f, 1.35f, 1.40f, 10f, 48f),
            Sti = new StiResult(0.68f, 4.3f, "Good"),
            DelayMatrix = delayMatrix
        };

        string md = ReportGenerator.GenerateMarkdown(data);

        Assert.Contains("**STI**", md);
        Assert.Contains("0.68 (Good)", md);
        Assert.Contains("%ALCons", md);
        Assert.Contains("4.3%", md);
        Assert.Contains("## 6. Multi-Zone Delay Matrix Alignment", md);
        Assert.Contains("Main PA", md);
        Assert.Contains("Delay Tower", md);
        Assert.Contains("+32.00 ms", md);
    }
}
