using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SoundCalibrator.Core.Reporting;

/// <summary>
/// Lightweight, zero-dependency PDF 1.4 report generator conforming to ISO 32000-1.
/// Produces professional, clean vector-drawn technical reports for acoustic calibration.
/// </summary>
public sealed class PdfReportGenerator
{
    private const float PageWidth = 595.28f;  // A4 width in points
    private const float PageHeight = 841.89f; // A4 height in points
    private const float MarginLeft = 40f;
    private const float MarginRight = 40f;
    private const float MarginTop = 40f;
    private const float MarginBottom = 45f;
    private const float ContentWidth = PageWidth - MarginLeft - MarginRight;

    private readonly List<StringBuilder> _pages = [];
    private StringBuilder _currentStream = new();
    private float _currentY = PageHeight - MarginTop;

    public byte[] Generate(CalibrationReportData data)
    {
        _pages.Clear();
        _currentStream = new StringBuilder();
        _currentY = PageHeight - MarginTop;

        // Render Report Document
        RenderHeader(data);
        RenderSystemDspConfig(data);

        if (data.Rt60.HasValue && data.Rt60.Value.IsValid)
        {
            RenderRoomAcoustics(data);
        }

        if (data.Etc != null && data.Etc.Reflections.Count > 0)
        {
            RenderEtcAnalysis(data);
        }

        if (data.Thd.HasValue && data.Thd.Value.FundamentalDb > -60f)
        {
            RenderDistortionAnalysis(data);
        }

        if (data.Imd != null && data.Imd.ImdPercent > 0.001f)
        {
            RenderImdAnalysis(data);
        }

        if (data.Alignment.HasValue)
        {
            RenderCrossoverAlignment(data);
        }

        if (data.PeqFilters != null && data.PeqFilters.Count > 0)
        {
            RenderPeqFilters(data);
        }

        if (data.DelayMatrix != null && data.DelayMatrix.Alignments.Count > 0)
        {
            RenderDelayMatrix(data);
        }

        if (data.Traces.Count > 0)
        {
            RenderStoredTraces(data);
        }

        // Commit final page
        _pages.Add(_currentStream);

        // Add footers with total page count
        for (int p = 0; p < _pages.Count; p++)
        {
            RenderFooter(_pages[p], p + 1, _pages.Count);
        }

        return BuildPdfBytes();
    }

    private void EnsureSpace(float requiredHeight)
    {
        if (_currentY - requiredHeight < MarginBottom)
        {
            _pages.Add(_currentStream);
            _currentStream = new StringBuilder();
            _currentY = PageHeight - MarginTop;
            RenderPageHeader();
        }
    }

    private void RenderPageHeader()
    {
        // Minimal top page banner for continuation pages
        _currentStream.AppendLine("0.08 0.11 0.18 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - 14} {ContentWidth} 14 re f"));
        
        _currentStream.AppendLine("BT");
        _currentStream.AppendLine("/F2 8 Tf");
        _currentStream.AppendLine("0.6 0.7 0.8 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft + 6} {_currentY - 10} Td"));
        _currentStream.AppendLine($"({EscapePdfText("SoundCalibrator Technical Report — Continued")}) Tj");
        _currentStream.AppendLine("ET");

        _currentY -= 24;
    }

    private void RenderHeader(CalibrationReportData data)
    {
        float bannerHeight = 54f;
        // Dark Cyan-Slate Header Banner
        _currentStream.AppendLine("0.06 0.09 0.15 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - bannerHeight} {ContentWidth} {bannerHeight} re f"));

        // Cyan Accent Strip on top
        _currentStream.AppendLine("0.0 0.85 0.95 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - 4} {ContentWidth} 4 re f"));

        // Title Text
        _currentStream.AppendLine("BT");
        _currentStream.AppendLine("/F2 16 Tf");
        _currentStream.AppendLine("1.0 1.0 1.0 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft + 12} {_currentY - 26} Td"));
        _currentStream.AppendLine($"({EscapePdfText("SOUNDCALIBRATOR — ACOUSTIC CALIBRATION REPORT")}) Tj");

        // Subtitle
        _currentStream.AppendLine("/F1 9 Tf");
        _currentStream.AppendLine("0.6 0.75 0.85 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"0 -15 Td"));
        _currentStream.AppendLine($"({EscapePdfText($"Project: {data.ProjectName}  |  Engineer: {data.EngineerName}  |  Date: {data.Timestamp:yyyy-MM-dd HH:mm:ss} UTC")}) Tj");
        _currentStream.AppendLine("ET");

        _currentY -= (bannerHeight + 16);
    }

    private void RenderSystemDspConfig(CalibrationReportData data)
    {
        RenderSectionTitle("1. System & DSP Configuration");

        string[][] rows =
        [
            ["Sample Rate", $"{data.SampleRate} Hz"],
            ["FFT Size", $"{data.FftSize} bins (Resolution: {(float)data.SampleRate / data.FftSize:0.0} Hz)"],
            ["Windowing Function", data.WindowType],
            ["Averaging Algorithm", data.AveragingType],
            ["Target Curve", data.TargetCurveName],
            ["Active Delay Compensation", $"{data.DelayCompensationMs:+0.00;-0.00;0.00} ms"],
            ["Polarity Inversion", data.InvertPolarity ? "Inverted (180 deg)" : "Normal (0 deg)"]
        ];

        RenderKeyValueTable(rows);
    }

    private void RenderRoomAcoustics(CalibrationReportData data)
    {
        var r = data.Rt60!.Value;
        RenderSectionTitle("2. Room Acoustics (ISO 3382 Reverberation & Intelligibility)");

        var rows = new List<string[]>
        {
            new[] { "Early Decay Time (EDT)", $"{r.EdtSeconds:0.00} s", "Initial energy decay (0 to -10 dB fit)" },
            new[] { "RT60 (T20)", $"{r.T20Seconds:0.00} s", "Standard RT60 extrapolation (-5 to -25 dB)" },
            new[] { "RT60 (T30)", $"{r.T30Seconds:0.00} s", "High dynamic range RT60 (-5 to -35 dB)" },
            new[] { "Decay Dynamic Range", $"{r.DynamicRangeDb:0.0} dB", "Signal-to-noise decay margin" }
        };

        if (data.Sti.HasValue)
        {
            var s = data.Sti.Value;
            rows.Add(["Speech Transmission Index (STI)", $"{s.Sti:0.00} ({s.Rating})", "Speech intelligibility metric (IEC 60268-16)"]);
            rows.Add(["%ALCons", $"{s.AlConsPercent:0.0}%", "Articulation Loss of Consonants (Peutz)"]);
        }

        RenderMultiColTable(["Acoustic Metric", "Measured Value", "Description"], [160, 110, 245], rows);
    }

    private void RenderEtcAnalysis(CalibrationReportData data)
    {
        var etc = data.Etc!;
        RenderSectionTitle("Early Reflection Analysis (Energy-Time Curve ETC)");

        var rows = new List<string[]>();
        for (int i = 0; i < etc.Reflections.Count; i++)
        {
            var refl = etc.Reflections[i];
            rows.Add([
                $"#{i + 1}",
                $"{refl.TimeMs:0.00} ms",
                $"+{refl.RelativeDelayMs:0.00} ms",
                $"{refl.LevelDb:+0.00;-0.00;0.00} dB",
                $"+{refl.PathDifferenceMeters:0.00} m"
            ]);
        }

        RenderMultiColTable(["#", "Arrival Time", "Relative Delay", "Level (dB)", "Path Diff"], [40, 115, 120, 120, 120], rows);
    }

    private void RenderDistortionAnalysis(CalibrationReportData data)
    {
        var t = data.Thd!.Value;
        RenderSectionTitle("3. Harmonic Distortion Analysis (THD)");

        string[][] rows =
        [
            ["Fundamental Frequency", $"{t.FundamentalFreqHz:0.#} Hz at {t.FundamentalDb:+0.00;-0.00;0.00} dBFS"],
            ["Total Harmonic Distortion (THD)", $"{t.ThdPercent:0.00}% ({t.ThdDb:0.0} dB)"],
            ["THD + Noise (THD+N)", $"{t.ThdPlusNPercent:0.00}% ({t.ThdPlusNDb:0.0} dB)"]
        ];

        RenderKeyValueTable(rows);
    }

    private void RenderImdAnalysis(CalibrationReportData data)
    {
        var imd = data.Imd!;
        RenderSectionTitle($"Intermodulation Distortion (IMD - {imd.Standard})");

        var rows = new List<string[]>
        {
            new[] { "Standard & Carrier", $"{imd.Standard} | Carrier: {imd.PrimaryToneDb:+0.00;-0.00;0.00} dBFS", "" },
            new[] { "Total IMD", $"{imd.ImdPercent:0.00}% ({imd.ImdDb:0.0} dB)", "" }
        };

        foreach (var p in imd.Products)
        {
            rows.Add([p.Name, $"{p.FrequencyHz:0.#} Hz", $"{p.LevelDb:+0.00;-0.00;0.00} dBFS"]);
        }

        RenderMultiColTable(["Product", "Frequency", "Level"], [160, 160, 195], rows);
    }

    private void RenderCrossoverAlignment(CalibrationReportData data)
    {
        var a = data.Alignment!.Value;
        RenderSectionTitle("4. Subwoofer + Main Crossover Phase Alignment");

        string[][] rows =
        [
            ["Crossover Frequency (Fc)", $"{a.CrossoverFreqHz:0} Hz"],
            ["Phase Difference (Delta Theta)", $"{a.PhaseDeltaDeg:+0.0;-0.0;0.0} deg"],
            ["Recommended Delay", $"{a.RecommendedDelayMs:+0.00;-0.00;0.00} ms ({a.RecommendedDistanceMeters:+0.00;-0.00;0.00} m)"],
            ["Recommended Polarity", a.RecommendPolarityInversion ? "INVERT POLARITY (180 deg)" : "NORMAL POLARITY (0 deg)"],
            ["Predicted Acoustic Summation", $"{a.PredictedSummationGainDb:+0.0;-0.0;0.0} dB"]
        ];

        RenderKeyValueTable(rows);
    }

    private void RenderPeqFilters(CalibrationReportData data)
    {
        RenderSectionTitle("5. Recommended Parametric EQ Filters (Auto PEQ)");

        var rows = new List<string[]>();
        for (int i = 0; i < data.PeqFilters!.Count; i++)
        {
            var f = data.PeqFilters[i];
            rows.Add([
                $"Band {i + 1}",
                $"{f.FrequencyHz:0.#} Hz",
                $"{f.GainDb:+0.00;-0.00;0.00} dB",
                $"{f.Q:0.00}",
                $"{f.BandwidthOctaves:0.00} oct"
            ]);
        }

        RenderMultiColTable(["Band", "Center Frequency", "Correction Gain", "Q Factor", "Bandwidth"], [85, 115, 115, 100, 100], rows);
    }

    private void RenderDelayMatrix(CalibrationReportData data)
    {
        var dm = data.DelayMatrix!;
        RenderSectionTitle($"6. Multi-Zone Delay Matrix (Anchor: {dm.AnchorZoneName}, Temp: {dm.TemperatureCelsius:0.#} C)");

        var rows = new List<string[]>();
        foreach (var a in dm.Alignments)
        {
            string sign = a.RequiredDelayOffsetMs >= 0 ? "+" : "";
            rows.Add([
                a.Name,
                $"{a.MeasuredDelayMs:0.00} ms",
                $"{sign}{a.RequiredDelayOffsetMs:0.00} ms",
                $"{sign}{a.RelativeDistanceMeters:0.00} m ({sign}{a.RelativeDistanceFeet:0.0} ft)"
            ]);
        }

        RenderMultiColTable(["Zone Name", "Arrival Delay", "Required Offset", "Relative Distance"], [140, 115, 120, 140], rows);
    }

    private void RenderStoredTraces(CalibrationReportData data)
    {
        RenderSectionTitle("7. Acoustic Traces in Session");

        var rows = new List<string[]>();
        for (int i = 0; i < data.Traces.Count; i++)
        {
            var tr = data.Traces[i];
            rows.Add([
                $"#{i + 1}",
                tr.Name,
                tr.HexColor,
                $"{tr.OffsetDb:+0;-0;0} dB",
                $"{tr.OffsetDelayMs:+0.00;-0.00;0.00} ms",
                tr.InvertPolarity ? "INV" : "NOR"
            ]);
        }

        RenderMultiColTable(["#", "Trace Name", "Color", "Gain Offset", "Delay Offset", "Polarity"], [35, 175, 75, 75, 85, 70], rows);
    }

    private void RenderSectionTitle(string title)
    {
        EnsureSpace(32f);

        _currentStream.AppendLine("BT");
        _currentStream.AppendLine("/F2 11 Tf");
        _currentStream.AppendLine("0.08 0.45 0.75 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - 12} Td"));
        _currentStream.AppendLine($"({EscapePdfText(title)}) Tj");
        _currentStream.AppendLine("ET");

        // Subtle underline rule
        _currentStream.AppendLine("0.85 0.90 0.95 RG");
        _currentStream.AppendLine("0.8 w");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - 15} m {MarginLeft + ContentWidth} {_currentY - 15} l S"));

        _currentY -= 22f;
    }

    private void RenderKeyValueTable(string[][] rows)
    {
        float rowHeight = 15f;
        EnsureSpace(rows.Length * rowHeight + 6);

        float col1Width = 190f;
        for (int i = 0; i < rows.Length; i++)
        {
            if (i % 2 == 1)
            {
                _currentStream.AppendLine("0.96 0.97 0.98 rg");
                _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - rowHeight} {ContentWidth} {rowHeight} re f"));
            }

            _currentStream.AppendLine("BT");
            _currentStream.AppendLine("/F2 8.5 Tf");
            _currentStream.AppendLine("0.2 0.25 0.35 rg");
            _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft + 6} {_currentY - 11} Td"));
            _currentStream.AppendLine($"({EscapePdfText(rows[i][0])}) Tj");

            _currentStream.AppendLine("/F1 8.5 Tf");
            _currentStream.AppendLine("0.1 0.15 0.2 rg");
            _currentStream.AppendLine(FormattableString.Invariant($"{col1Width} 0 Td"));
            _currentStream.AppendLine($"({EscapePdfText(rows[i][1])}) Tj");
            _currentStream.AppendLine("ET");

            _currentY -= rowHeight;
        }

        _currentY -= 8f;
    }

    private void RenderMultiColTable(string[] headers, float[] colWidths, IReadOnlyList<string[]> rows)
    {
        float headerHeight = 16f;
        float rowHeight = 15f;
        EnsureSpace(headerHeight + Math.Min(rows.Count, 3) * rowHeight + 8);

        // Header Background
        _currentStream.AppendLine("0.14 0.18 0.26 rg");
        _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - headerHeight} {ContentWidth} {headerHeight} re f"));

        // Header Text
        _currentStream.AppendLine("BT");
        _currentStream.AppendLine("/F2 8 Tf");
        _currentStream.AppendLine("0.95 0.98 1.0 rg");
        float curX = MarginLeft + 6;
        for (int c = 0; c < headers.Length; c++)
        {
            if (c == 0)
                _currentStream.AppendLine(FormattableString.Invariant($"{curX} {_currentY - 11} Td"));
            else
                _currentStream.AppendLine(FormattableString.Invariant($"{colWidths[c - 1]} 0 Td"));

            _currentStream.AppendLine($"({EscapePdfText(headers[c])}) Tj");
            curX += colWidths[c];
        }
        _currentStream.AppendLine("ET");

        _currentY -= headerHeight;

        // Data Rows
        for (int i = 0; i < rows.Count; i++)
        {
            EnsureSpace(rowHeight + 4);

            if (i % 2 == 1)
            {
                _currentStream.AppendLine("0.96 0.97 0.98 rg");
                _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {_currentY - rowHeight} {ContentWidth} {rowHeight} re f"));
            }

            _currentStream.AppendLine("BT");
            _currentStream.AppendLine("/F1 8 Tf");
            _currentStream.AppendLine("0.1 0.15 0.2 rg");

            var row = rows[i];
            for (int c = 0; c < row.Length && c < colWidths.Length; c++)
            {
                if (c == 0)
                    _currentStream.AppendLine(FormattableString.Invariant($"{MarginLeft + 6} {_currentY - 11} Td"));
                else
                    _currentStream.AppendLine(FormattableString.Invariant($"{colWidths[c - 1]} 0 Td"));

                _currentStream.AppendLine($"({EscapePdfText(row[c])}) Tj");
            }
            _currentStream.AppendLine("ET");

            _currentY -= rowHeight;
        }

        _currentY -= 8f;
    }

    private static void RenderFooter(StringBuilder pageStream, int pageNumber, int totalPages)
    {
        // Footer line
        pageStream.AppendLine("0.85 0.88 0.92 RG");
        pageStream.AppendLine("0.6 w");
        pageStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {MarginBottom + 12} m {MarginLeft + ContentWidth} {MarginBottom + 12} l S"));

        // Footer Text
        pageStream.AppendLine("BT");
        pageStream.AppendLine("/F1 8 Tf");
        pageStream.AppendLine("0.45 0.5 0.6 rg");
        pageStream.AppendLine(FormattableString.Invariant($"{MarginLeft} {MarginBottom} Td"));
        pageStream.AppendLine($"({EscapePdfText("SoundCalibrator Core .NET 10 | Acoustic Engine")}) Tj");

        pageStream.AppendLine("/F2 8 Tf");
        pageStream.AppendLine(FormattableString.Invariant($"{ContentWidth - 60} 0 Td"));
        pageStream.AppendLine($"({EscapePdfText($"Page {pageNumber} of {totalPages}")}) Tj");
        pageStream.AppendLine("ET");
    }

    private static string EscapePdfText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length + 10);
        foreach (char c in text)
        {
            switch (c)
            {
                case '(': sb.Append(@"\("); break;
                case ')': sb.Append(@"\)"); break;
                case '\\': sb.Append(@"\\"); break;
                default:
                    // Only printable ASCII range for standard Type 1 fonts
                    if (c >= 32 && c <= 126)
                        sb.Append(c);
                    else
                    {
                        // Transliterate common symbols
                        switch (c)
                        {
                            case '°': sb.Append(" deg"); break;
                            case 'Δ': sb.Append("Delta"); break;
                            case 'θ': sb.Append("theta"); break;
                            case 'Σ': sb.Append("SUM"); break;
                            case '—': sb.Append(" - "); break;
                            case '–': sb.Append("-"); break;
                            case 'μ': sb.Append("u"); break;
                            case '±': sb.Append("+/-"); break;
                            default: sb.Append(' '); break;
                        }
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private byte[] BuildPdfBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        var offsets = new List<long>();

        void WriteObj(int objNum, string body)
        {
            writer.Flush();
            offsets.Add(ms.Position);
            writer.Write($"{objNum} 0 obj\n{body}\nendobj\n");
        }

        // Header
        writer.Write("%PDF-1.4\n%\x80\x81\x82\x83\n");

        int totalPages = _pages.Count;
        int catalogObj = 1;
        int pagesObj = 2;
        int fontF1Obj = 3;
        int fontF2Obj = 4;
        int fontF3Obj = 5;

        // Reserve obj IDs:
        // 1: Catalog
        // 2: Pages
        // 3: F1 (Helvetica)
        // 4: F2 (Helvetica-Bold)
        // 5: F3 (Courier)
        // For each page i (0-based):
        // Page obj: 6 + (i * 2)
        // Content stream obj: 7 + (i * 2)

        // 1: Catalog
        WriteObj(catalogObj, $"<< /Type /Catalog /Pages {pagesObj} 0 R >>");

        // 2: Pages list placeholder
        var kids = new StringBuilder();
        for (int i = 0; i < totalPages; i++)
        {
            int pageObjNum = 6 + (i * 2);
            kids.Append($"{pageObjNum} 0 R ");
        }
        WriteObj(pagesObj, $"<< /Type /Pages /Kids [{kids.ToString().Trim()}] /Count {totalPages} >>");

        // Fonts
        WriteObj(fontF1Obj, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        WriteObj(fontF2Obj, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        WriteObj(fontF3Obj, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");

        // Page and Stream objects
        for (int i = 0; i < totalPages; i++)
        {
            int pageObjNum = 6 + (i * 2);
            int contentObjNum = 7 + (i * 2);

            string pageContent = _pages[i].ToString();
            byte[] contentBytes = Encoding.ASCII.GetBytes(pageContent);

            // Page Object
            WriteObj(pageObjNum,
                $"<< /Type /Page /Parent {pagesObj} 0 R " +
                $"/MediaBox [0 0 {PageWidth:0.00} {PageHeight:0.00}] " +
                $"/Contents {contentObjNum} 0 R " +
                $"/Resources << /Font << /F1 {fontF1Obj} 0 R /F2 {fontF2Obj} 0 R /F3 {fontF3Obj} 0 R >> >> >>");

            // Content Stream Object
            writer.Flush();
            offsets.Add(ms.Position);
            writer.Write($"{contentObjNum} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            writer.Write("\nendstream\nendobj\n");
        }

        // Cross-Reference Table
        writer.Flush();
        long xrefPos = ms.Position;
        int totalObjects = 5 + (totalPages * 2);

        writer.Write("xref\n");
        writer.Write($"0 {totalObjects + 1}\n");
        writer.Write("0000000000 65535 f \n");
        for (int i = 0; i < offsets.Count; i++)
        {
            writer.Write($"{offsets[i]:D10} 00000 n \n");
        }

        // Trailer
        writer.Write($"trailer\n<< /Size {totalObjects + 1} /Root {catalogObj} 0 R >>\n");
        writer.Write($"startxref\n{xrefPos}\n%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }
}
