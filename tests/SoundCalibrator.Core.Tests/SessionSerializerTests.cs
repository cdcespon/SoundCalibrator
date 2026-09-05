using System;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Serialization;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SessionSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundtripsFullSessionAccurately()
    {
        var trace1 = new AcousticTrace("Subwoofer", "#FF0000", [40f, 80f], [6f, 3f], [0f, -90f], [0.95f, 0.98f])
        {
            OffsetDb = -2f,
            OffsetDelayMs = 1.25f,
            InvertPolarity = true,
            DetectedDelayMs = 12.4f,
            IsRtaTrace = false
        };

        var trace2 = new AcousticTrace("RTA Peak", "#FFD600", [1000f], [-12f], [0f], [1f])
        {
            IsRtaTrace = true
        };

        var session = new ProjectSession
        {
            ProjectName = "Luna Park Arena Soundcheck",
            EngineerName = "Carlos Cespon",
            VenueName = "Main Hall",
            SampleRate = 48000,
            FftSize = 2048,
            WindowType = "BlackmanHarris",
            AveragingType = "Linear16",
            DelayCompensationMs = -5.4f,
            InvertPolarity = true,
            SplOffsetDb = 94.2f,
            TargetCurvePresetName = "HarmanTarget",
            StoredTraces = [
                AcousticTraceDto.FromModel(trace1),
                AcousticTraceDto.FromModel(trace2)
            ]
        };

        string json = SessionSerializer.Serialize(session);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var restored = SessionSerializer.Deserialize(json);
        Assert.NotNull(restored);

        Assert.Equal("Luna Park Arena Soundcheck", restored.ProjectName);
        Assert.Equal("Carlos Cespon", restored.EngineerName);
        Assert.Equal("Main Hall", restored.VenueName);
        Assert.Equal(48000, restored.SampleRate);
        Assert.Equal(2048, restored.FftSize);
        Assert.Equal("BlackmanHarris", restored.WindowType);
        Assert.Equal("Linear16", restored.AveragingType);
        Assert.Equal(-5.4f, restored.DelayCompensationMs);
        Assert.True(restored.InvertPolarity);
        Assert.Equal(94.2f, restored.SplOffsetDb);
        Assert.Equal("HarmanTarget", restored.TargetCurvePresetName);

        Assert.Equal(2, restored.StoredTraces.Count);

        var restoredTrace1 = restored.StoredTraces[0].ToModel();
        Assert.Equal("Subwoofer", restoredTrace1.Name);
        Assert.Equal("#FF0000", restoredTrace1.HexColor);
        Assert.Equal(-2f, restoredTrace1.OffsetDb);
        Assert.Equal(1.25f, restoredTrace1.OffsetDelayMs);
        Assert.True(restoredTrace1.InvertPolarity);
        Assert.Equal(12.4f, restoredTrace1.DetectedDelayMs);
        Assert.False(restoredTrace1.IsRtaTrace);
        Assert.Equal(2, restoredTrace1.Frequencies.Length);
        Assert.Equal(6f, restoredTrace1.MagnitudeDb[0]);
        Assert.Equal(-90f, restoredTrace1.PhaseDegrees[1]);
        Assert.Equal(0.98f, restoredTrace1.Coherence[1]);

        var restoredTrace2 = restored.StoredTraces[1].ToModel();
        Assert.Equal("RTA Peak", restoredTrace2.Name);
        Assert.True(restoredTrace2.IsRtaTrace);
    }

    [Fact]
    public void Deserialize_InvalidOrEmpty_ReturnsNull()
    {
        Assert.Null(SessionSerializer.Deserialize(""));
        Assert.Null(SessionSerializer.Deserialize("   "));
    }
}
