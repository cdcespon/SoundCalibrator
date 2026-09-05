using System;
using System.Collections.Generic;
using System.Text;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Reporting;

public sealed class CalibrationReportData
{
    public string ProjectName { get; set; } = "SoundCalibrator Measurement Session";
    public string EngineerName { get; set; } = "Audio Engineer";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int SampleRate { get; set; } = 48000;
    public int FftSize { get; set; } = 1024;
    public string WindowType { get; set; } = "Hann";
    public string AveragingType { get; set; } = "ExponentialFast";
    public string TargetCurveName { get; set; } = "None";
    public float DelayCompensationMs { get; set; } = 0f;
    public bool InvertPolarity { get; set; } = false;
    public ReverberationTimeResult? Rt60 { get; set; }
    public StiResult? Sti { get; set; }
    public ThdResult? Thd { get; set; }
    public ImdResult? Imd { get; set; }
    public EtcResult? Etc { get; set; }
    public AlignmentSuggestion? Alignment { get; set; }
    public DelayMatrixReport? DelayMatrix { get; set; }
    public IReadOnlyList<AcousticTrace> Traces { get; set; } = [];
    public IReadOnlyList<PeqFilterSuggestion>? PeqFilters { get; set; }
}

public static class ReportGenerator
{
    public static string GenerateMarkdown(CalibrationReportData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Acoustic Calibration Report - {data.ProjectName}");
        sb.AppendLine($"**Engineer:** {data.EngineerName} | **Date:** {data.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Engine:** SoundCalibrator (.NET 10 Audio Engine)");
        sb.AppendLine();

        sb.AppendLine("## 1. System & DSP Configuration");
        sb.AppendLine("| Parameter | Value |");
        sb.AppendLine("| :--- | :--- |");
        sb.AppendLine($"| Sample Rate | {data.SampleRate} Hz |");
        sb.AppendLine($"| FFT Size | {data.FftSize} bins (Resolution: {(float)data.SampleRate / data.FftSize:0.0} Hz) |");
        sb.AppendLine($"| Windowing | {data.WindowType} |");
        sb.AppendLine($"| Averaging | {data.AveragingType} |");
        sb.AppendLine($"| Target Curve | {data.TargetCurveName} |");
        sb.AppendLine($"| Active Delay Compensation | {data.DelayCompensationMs:+0.00;-0.00;0.00} ms |");
        sb.AppendLine($"| Polarity Inversion | {(data.InvertPolarity ? "Inverted (180°)" : "Normal")} |");
        sb.AppendLine();

        if (data.Rt60.HasValue && data.Rt60.Value.IsValid)
        {
            var r = data.Rt60.Value;
            sb.AppendLine("## 2. Room Acoustics (Reverberation Time ISO 3382)");
            sb.AppendLine("| Metric | Value | Description |");
            sb.AppendLine("| :--- | :--- | :--- |");
            sb.AppendLine($"| **EDT** | {r.EdtSeconds:0.00}s | Early Decay Time (0 to -10 dB fit) |");
            sb.AppendLine($"| **T20 (RT60)** | {r.T20Seconds:0.00}s | Standard RT60 extrapolation (-5 to -25 dB fit) |");
            sb.AppendLine($"| **T30 (RT60)** | {r.T30Seconds:0.00}s | High dynamic range RT60 (-5 to -35 dB fit) |");
            sb.AppendLine($"| Dynamic Range | {r.DynamicRangeDb:0.0} dB | Signal to noise decay margin |");
            if (data.Sti.HasValue)
            {
                var s = data.Sti.Value;
                sb.AppendLine($"| **STI** | {s.Sti:0.00} ({s.Rating}) | Speech Transmission Index (IEC 60268-16) |");
                sb.AppendLine($"| **%ALCons** | {s.AlConsPercent:0.0}% | Articulation Loss of Consonants (Farah/Peutz) |");
            }
            sb.AppendLine();
        }

        if (data.Etc != null && data.Etc.Reflections.Count > 0)
        {
            var etc = data.Etc;
            sb.AppendLine("## Early Reflection Analysis (Energy-Time Curve ETC)");
            sb.AppendLine($"* **Direct Sound Arrival:** {etc.DirectSoundTimeMs:0.00} ms");
            sb.AppendLine("| # | Arrival Time | Relative Delay (\u0394t) | Relative Level | Path Difference (\u0394d) |");
            sb.AppendLine("| :-: | :--- | :--- | :--- | :--- |");
            for (int i = 0; i < etc.Reflections.Count; i++)
            {
                var refl = etc.Reflections[i];
                sb.AppendLine($"| {i + 1} | {refl.TimeMs:0.00} ms | +{refl.RelativeDelayMs:0.00} ms | {refl.LevelDb:+0.00;-0.00;0.00} dB | +{refl.PathDifferenceMeters:0.00} m |");
            }
            sb.AppendLine();
        }

        if (data.Thd.HasValue && data.Thd.Value.FundamentalDb > -60f)
        {
            var t = data.Thd.Value;
            sb.AppendLine("## 3. Harmonic Distortion Analysis (THD)");
            sb.AppendLine($"* **Fundamental:** {t.FundamentalFreqHz:0.#} Hz at {t.FundamentalDb:+0.00;-0.00;0.00} dBFS");
            sb.AppendLine($"* **THD:** {t.ThdPercent:0.00}% ({t.ThdDb:0.0} dB)");
            sb.AppendLine($"* **THD+N:** {t.ThdPlusNPercent:0.00}% ({t.ThdPlusNDb:0.0} dB)");
            sb.AppendLine();
        }

        if (data.Imd != null && data.Imd.ImdPercent > 0.001f)
        {
            var imd = data.Imd;
            sb.AppendLine($"## Intermodulation Distortion (IMD - {imd.Standard})");
            sb.AppendLine($"* **Standard:** {imd.Standard} | **Carrier / Primary:** {imd.PrimaryToneDb:+0.00;-0.00;0.00} dBFS");
            sb.AppendLine($"* **Total IMD:** {imd.ImdPercent:0.00}% ({imd.ImdDb:0.0} dB)");
            sb.AppendLine("| Product | Frequency | Level (dBFS) |");
            sb.AppendLine("| :--- | :--- | :--- |");
            foreach (var p in imd.Products)
            {
                sb.AppendLine($"| {p.Name} | {p.FrequencyHz:0.#} Hz | {p.LevelDb:+0.00;-0.00;0.00} dBFS |");
            }
            sb.AppendLine();
        }

        if (data.Alignment.HasValue)
        {
            var a = data.Alignment.Value;
            sb.AppendLine("## 4. Subwoofer + Main Crossover Phase Alignment");
            sb.AppendLine($"* **Crossover Frequency (Fc):** {a.CrossoverFreqHz:0} Hz");
            sb.AppendLine($"* **Phase Difference (Δθ):** {a.PhaseDeltaDeg:+0.0;-0.0;0.0}°");
            sb.AppendLine($"* **Recommended Delay:** {a.RecommendedDelayMs:+0.00;-0.00;0.00} ms ({a.RecommendedDistanceMeters:+0.00;-0.00;0.00} m)");
            sb.AppendLine($"* **Recommended Polarity:** {(a.RecommendPolarityInversion ? "INVERT Ø" : "NORMAL Ø")}");
            sb.AppendLine($"* **Predicted Acoustic Sum:** {a.PredictedSummationGainDb:+0.0;-0.0;0.0} dB");
            sb.AppendLine();
        }

        if (data.PeqFilters != null && data.PeqFilters.Count > 0)
        {
            sb.AppendLine("## 5. Recommended Parametric EQ Filters (Auto PEQ)");
            sb.AppendLine("| Band | Center Frequency | Correction Gain | Q Factor | Bandwidth |");
            sb.AppendLine("| :-: | :--- | :--- | :--- | :--- |");
            for (int i = 0; i < data.PeqFilters.Count; i++)
            {
                var f = data.PeqFilters[i];
                sb.AppendLine($"| {i + 1} | {f.FrequencyHz:0.#} Hz | {f.GainDb:+0.00;-0.00;0.00} dB | {f.Q:0.00} | {f.BandwidthOctaves:0.00} oct |");
            }
            sb.AppendLine();
        }

        if (data.DelayMatrix != null && data.DelayMatrix.Alignments.Count > 0)
        {
            var dm = data.DelayMatrix;
            sb.AppendLine("## 6. Multi-Zone Delay Matrix Alignment");
            sb.AppendLine($"* **Anchor Zone:** {dm.AnchorZoneName} | **Temperature:** {dm.TemperatureCelsius:0.#} \u00B0C | **Speed of Sound:** {dm.SpeedOfSoundMps:0.#} m/s");
            sb.AppendLine("| Zone | Measured Arrival | Required Offset | Relative Distance |");
            sb.AppendLine("| :--- | :--- | :--- | :--- |");
            foreach (var a in dm.Alignments)
            {
                string sign = a.RequiredDelayOffsetMs >= 0 ? "+" : "";
                sb.AppendLine($"| {a.Name} | {a.MeasuredDelayMs:0.00} ms | {sign}{a.RequiredDelayOffsetMs:0.00} ms | {sign}{a.RelativeDistanceMeters:0.00} m ({sign}{a.RelativeDistanceFeet:0.0} ft) |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 7. Stored Traces");
        if (data.Traces.Count == 0)
        {
            sb.AppendLine("*No individual traces stored in this session.*");
        }
        else
        {
            sb.AppendLine("| # | Name | Color | Offset dB | Delay ms | Polarity |");
            sb.AppendLine("| :-: | :--- | :--- | :-: | :-: | :-: |");
            for (int i = 0; i < data.Traces.Count; i++)
            {
                var tr = data.Traces[i];
                sb.AppendLine($"| {i + 1} | {tr.Name} | {tr.HexColor} | {tr.OffsetDb:+0;-0;0} dB | {tr.OffsetDelayMs:+0.00;-0.00;0.00} ms | {(tr.InvertPolarity ? "INV" : "NOR")} |");
            }
        }

        return sb.ToString();
    }

    public static string GenerateText(CalibrationReportData data)
    {
        return GenerateMarkdown(data);
    }

    public static byte[] GeneratePdf(CalibrationReportData data)
    {
        var pdfGenerator = new PdfReportGenerator();
        return pdfGenerator.Generate(data);
    }

    public static string GenerateHtml(CalibrationReportData data)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8""/>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
    <title>SoundCalibrator Report - {data.ProjectName}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: #0d1117;
            color: #c9d1d9;
            margin: 40px auto;
            max-width: 900px;
            padding: 0 20px;
            line-height: 1.6;
        }}
        h1 {{ color: #58a6ff; border-bottom: 1px solid #21262d; padding-bottom: 10px; }}
        h2 {{ color: #79c0ff; margin-top: 30px; border-bottom: 1px solid #30363d; padding-bottom: 6px; font-size: 1.3em; }}
        table {{ border-collapse: collapse; width: 100%; margin: 16px 0; }}
        th, td {{ border: 1px solid #30363d; padding: 8px 12px; text-align: left; }}
        th {{ background-color: #161b22; color: #f0f6fc; }}
        tr:nth-child(even) {{ background-color: #161b22; }}
        .badge {{ display: inline-block; padding: 2px 8px; border-radius: 4px; font-weight: bold; font-size: 0.9em; }}
        .badge-green {{ background: #238636; color: #fff; }}
        .badge-cyan {{ background: #1f6feb; color: #fff; }}
        @media print {{
            body {{ background-color: #fff; color: #000; }}
            th {{ background-color: #eee; color: #000; }}
            h1, h2 {{ color: #000; }}
        }}
    </style>
</head>
<body>
    <h1>🔊 SoundCalibrator Acoustic Report</h1>
    <p><strong>Project:</strong> {data.ProjectName} &nbsp;|&nbsp; <strong>Date:</strong> {data.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</p>
    <pre style=""font-family: inherit; white-space: pre-wrap;"">{GenerateMarkdown(data)}</pre>
</body>
</html>";
    }
}

