using System;
using System.Collections.Generic;
using System.Text.Json;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Serialization;

public sealed class AcousticTraceDto
{
    public string Name { get; set; } = "";
    public string HexColor { get; set; } = "#00E5FF";
    public bool IsVisible { get; set; } = true;
    public float OffsetDb { get; set; } = 0f;
    public float OffsetDelayMs { get; set; } = 0f;
    public bool InvertPolarity { get; set; } = false;
    public float DetectedDelayMs { get; set; } = 0f;
    public bool IsRtaTrace { get; set; } = false;
    public float[] Frequencies { get; set; } = [];
    public float[] MagnitudeDb { get; set; } = [];
    public float[] PhaseDegrees { get; set; } = [];
    public float[] Coherence { get; set; } = [];

    public AcousticTrace ToModel()
    {
        return new AcousticTrace(Name, HexColor, Frequencies, MagnitudeDb, PhaseDegrees, Coherence)
        {
            IsVisible = IsVisible,
            OffsetDb = OffsetDb,
            OffsetDelayMs = OffsetDelayMs,
            InvertPolarity = InvertPolarity,
            DetectedDelayMs = DetectedDelayMs,
            IsRtaTrace = IsRtaTrace
        };
    }

    public static AcousticTraceDto FromModel(AcousticTrace trace)
    {
        return new AcousticTraceDto
        {
            Name = trace.Name,
            HexColor = trace.HexColor,
            IsVisible = trace.IsVisible,
            OffsetDb = trace.OffsetDb,
            OffsetDelayMs = trace.OffsetDelayMs,
            InvertPolarity = trace.InvertPolarity,
            DetectedDelayMs = trace.DetectedDelayMs,
            IsRtaTrace = trace.IsRtaTrace,
            Frequencies = (float[])trace.Frequencies.Clone(),
            MagnitudeDb = (float[])trace.MagnitudeDb.Clone(),
            PhaseDegrees = (float[])trace.PhaseDegrees.Clone(),
            Coherence = (float[])trace.Coherence.Clone()
        };
    }
}

public sealed class ProjectSession
{
    public string ProjectName { get; set; } = "SoundCalibrator Session";
    public string EngineerName { get; set; } = "Audio Engineer";
    public string VenueName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int SampleRate { get; set; } = 48000;
    public int FftSize { get; set; } = 1024;
    public string WindowType { get; set; } = "Hann";
    public string AveragingType { get; set; } = "ExponentialFast";
    public float DelayCompensationMs { get; set; } = 0f;
    public bool InvertPolarity { get; set; } = false;
    public float SplOffsetDb { get; set; } = 0f;
    public string? TargetCurvePresetName { get; set; }
    public List<AcousticTraceDto> StoredTraces { get; set; } = [];
}

public static class SessionSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(ProjectSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return JsonSerializer.Serialize(session, JsonOptions);
    }

    public static ProjectSession? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<ProjectSession>(json, JsonOptions);
    }
}
