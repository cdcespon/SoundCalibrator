using System;
using System.Collections.Generic;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class AcousticDelayMatrixTests
{
    [Fact]
    public void CalculateSpeedOfSound_StandardTemperatures_AreAccurate()
    {
        float c0 = AcousticDelayMatrix.CalculateSpeedOfSound(0f);
        Assert.Equal(331.3f, c0, precision: 1);

        float c20 = AcousticDelayMatrix.CalculateSpeedOfSound(20f);
        Assert.InRange(c20, 343.0f, 343.5f);

        float c35 = AcousticDelayMatrix.CalculateSpeedOfSound(35f);
        Assert.InRange(c35, 351.5f, 352.5f);
    }

    [Fact]
    public void CalculateAlignmentMatrix_MainPAAndDelayTower_CalculatesCorrectOffsets()
    {
        var zones = new List<(string Name, float DelayMs)>
        {
            ("Main PA", 60.0f),     // Anchor (llega a 60ms al punto de transición)
            ("Delay Tower 1", 20.0f) // Llega a 20ms desde la torre
        };

        var report = AcousticDelayMatrix.CalculateAlignmentMatrix(zones, anchorIndex: 0, temperatureCelsius: 20f);

        Assert.Equal("Main PA", report.AnchorZoneName);
        Assert.Equal(2, report.Alignments.Count);

        // Main PA con respecto a sí mismo debe tener 0 delay
        var main = report.Alignments[0];
        Assert.Equal(0f, main.RequiredDelayOffsetMs, precision: 2);
        Assert.Equal(0f, main.RelativeDistanceMeters, precision: 2);

        // Delay Tower 1 debe demorarse +40 ms para esperar a que llegue la onda del Main PA
        var tower = report.Alignments[1];
        Assert.Equal(40.0f, tower.RequiredDelayOffsetMs, precision: 2);
        Assert.InRange(tower.RelativeDistanceMeters, 13.5f, 14.0f);
        Assert.InRange(tower.RelativeDistanceFeet, 44.0f, 46.0f);
    }

    [Fact]
    public void CalculateAlignmentMatrix_EmptyList_ReturnsEmptyGracefully()
    {
        var report = AcousticDelayMatrix.CalculateAlignmentMatrix(Array.Empty<(string, float)>());
        Assert.Equal("None", report.AnchorZoneName);
        Assert.Empty(report.Alignments);
    }
}
