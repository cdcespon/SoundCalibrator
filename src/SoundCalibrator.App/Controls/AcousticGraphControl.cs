using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SoundCalibrator.Audio.Engine;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.DSP;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SoundCalibrator.App.Controls;

public sealed class AcousticGraphControl : Control
{
    private MeasurementSnapshot? _currentSnapshot;
    private Point? _mousePosition;

    public List<AcousticTrace> StoredTraces { get; } = [];
    public float CoherenceThreshold { get; set; } = 0.0f;
    public TargetCurve? ActiveTargetCurve { get; set; }
    public bool ShowDeltaCurve { get; set; } = false;
    public bool ShowPeqPreview { get; set; } = false;
    public IReadOnlyList<PeqFilterSuggestion>? ActivePeqFilters { get; set; }
    public bool IsSpectrogramMode { get; set; } = false;
    public bool ShowGroupDelay { get; set; } = false;
    public bool RtaBarMode { get; set; } = false;
    public bool ShowImpulseEtc { get; set; } = false;
    public bool ShowMinimumPhase { get; set; } = false;
    private SpectrogramBuffer? _spectrogramBuffer;
    private WriteableBitmap? _spectrogramBmp;

    // Paleta de diseño
    private static readonly Color BgColor = Color.Parse("#101318");
    private static readonly Color GridColor = Color.Parse("#1F2530");
    private static readonly Color TextColor = Color.Parse("#7E8B9B");
    private static readonly Color MagLineColor = Color.Parse("#00E5FF"); // Cyan
    private static readonly Color PhaseLineColor = Color.Parse("#FF9100"); // Naranja
    private static readonly Color CohLineColor = Color.Parse("#00E676"); // Verde esmeralda
    private static readonly Color RtaLiveColor = Color.Parse("#FFD600"); // Amarillo dorado
    private static readonly Color RtaMaxColor = Color.Parse("#FF3D00"); // Rojo coral
    private static readonly Color CrosshairColor = Color.Parse("#50FFFFFF");

    private static readonly Typeface LabelFont = new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

    public void UpdateSnapshot(MeasurementSnapshot snapshot)
    {
        _currentSnapshot = snapshot;
        if (_spectrogramBuffer == null || _spectrogramBuffer.BinCount != snapshot.BinCount)
        {
            _spectrogramBuffer = new SpectrogramBuffer(capacity: 100, binCount: snapshot.BinCount);
        }
        _spectrogramBuffer.PushFrame(snapshot.MagnitudeDb);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _mousePosition = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _mousePosition = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        double w = bounds.Width;
        double h = bounds.Height;
        if (w <= 10 || h <= 10) return;

        context.FillRectangle(new SolidColorBrush(BgColor), bounds);

        if (ShowImpulseEtc && _currentSnapshot != null && _currentSnapshot.ImpulseResponse.Length > 0)
        {
            DrawImpulseAndEtc(context, w, h, _currentSnapshot);
            if (_mousePosition.HasValue)
            {
                DrawEtcCrosshair(context, w, h, _mousePosition.Value, _currentSnapshot);
            }
            return;
        }

        if (IsSpectrogramMode && _spectrogramBuffer != null && _currentSnapshot != null)
        {
            DrawSpectrogram(context, w, h, _spectrogramBuffer, _currentSnapshot.Frequencies);
            DrawFrequencyGrid(context, w, h * 0.95, h * 0.95, 0);
            if (_mousePosition.HasValue)
            {
                DrawCrosshairAndReadout(context, w, h * 0.95, h * 0.95, 0, _mousePosition.Value, _currentSnapshot, true);
            }
            return;
        }

        bool isRta = _currentSnapshot?.IsRtaMode ?? false;

        double mainH = isRta ? h * 0.95 : h * 0.75;
        double cohH = isRta ? 0 : h * 0.25;
        double cohTop = mainH;

        DrawFrequencyGrid(context, w, mainH, cohTop, cohH);

        if (isRta)
        {
            DrawRtaGrid(context, w, mainH);

            foreach (var trace in StoredTraces)
            {
                if (trace.IsVisible && trace.IsRtaTrace)
                {
                    DrawStoredRtaTrace(context, w, mainH, trace);
                }
            }

            if (_currentSnapshot != null)
            {
                if (RtaBarMode)
                {
                    DrawRtaBars(context, w, mainH, _currentSnapshot);
                }
                else
                {
                    DrawRtaCurves(context, w, mainH, _currentSnapshot);
                }
            }
        }
        else
        {
            DrawMagnitudeAndPhaseGrid(context, w, mainH);
            DrawCoherenceGrid(context, w, cohTop, cohH);

            foreach (var trace in StoredTraces)
            {
                if (trace.IsVisible)
                {
                    DrawStoredTrace(context, w, mainH, cohTop, cohH, trace);
                }
            }

            if (_currentSnapshot != null)
            {
                DrawDataCurves(context, w, mainH, cohTop, cohH, _currentSnapshot);
            }
        }

        if (ActiveTargetCurve != null && ActiveTargetCurve.Points.Count > 0)
        {
            DrawTargetCurve(context, w, mainH, ActiveTargetCurve);

            if (ShowDeltaCurve && _currentSnapshot != null)
            {
                DrawDeltaCurve(context, w, mainH, _currentSnapshot, ActiveTargetCurve);
            }
        }

        if (ShowPeqPreview && ActivePeqFilters != null && ActivePeqFilters.Count > 0 && _currentSnapshot != null)
        {
            DrawPeqPreview(context, w, mainH, _currentSnapshot, ActivePeqFilters);
        }

        if (_mousePosition.HasValue && _currentSnapshot != null)
        {
            DrawCrosshairAndReadout(context, w, mainH, cohTop, cohH, _mousePosition.Value, _currentSnapshot, isRta);
        }
    }

    private void DrawRtaGrid(DrawingContext context, double w, double mainH)
    {
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1, DashStyle.Dash);

        // dBFS: 0 dBFS hasta -96 dBFS (paso 12 dB)
        for (float db = -96f; db <= 0f; db += 12f)
        {
            double y = RtaDbToY(db, mainH);
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));

            var label = new FormattedText(
                $"{db:0} dBFS",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                10,
                new SolidColorBrush(RtaLiveColor));
            context.DrawText(label, new Point(5, y - 12));
        }
    }

    private void DrawRtaCurves(DrawingContext context, double w, double mainH, MeasurementSnapshot snap)
    {
        var rtaGeom = new StreamGeometry();
        var maxGeom = new StreamGeometry();
        bool rtaStarted = false;
        bool maxStarted = false;

        using (var rtaCtx = rtaGeom.Open())
        using (var maxCtx = maxGeom.Open())
        {
            for (int i = 1; i < snap.Frequencies.Length; i++)
            {
                float freq = snap.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                double x = FreqToX(freq, w);
                double yRta = RtaDbToY(snap.MagnitudeDb[i], mainH);
                double yMax = RtaDbToY(snap.RtaMaxHoldDb[i], mainH);

                if (!rtaStarted)
                {
                    rtaCtx.BeginFigure(new Point(x, yRta), false);
                    rtaStarted = true;
                }
                else
                {
                    rtaCtx.LineTo(new Point(x, yRta));
                }

                if (!maxStarted)
                {
                    maxCtx.BeginFigure(new Point(x, yMax), false);
                    maxStarted = true;
                }
                else
                {
                    maxCtx.LineTo(new Point(x, yMax));
                }
            }
        }

        if (maxStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(RtaMaxColor), 1.4, DashStyle.Dash), maxGeom);
        }
        if (rtaStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(RtaLiveColor), 2.2), rtaGeom);
        }
    }

    private void DrawRtaBars(DrawingContext context, double w, double mainH, MeasurementSnapshot snap)
    {
        Span<float> bandLevels = stackalloc float[31];
        int bands = OctaveBandRtaCalculator.CalculateBands(snap.Frequencies, snap.MagnitudeDb, OctaveBandResolution.ThirdOctave, bandLevels);
        if (bands == 0) return;

        double slotWidth = w / bands;
        double barWidth = Math.Max(2.0, slotWidth - 3.0);
        var barBrush = new SolidColorBrush(Color.Parse("#FFD600"));
        var barPen = new Pen(new SolidColorBrush(Color.Parse("#FFA000")), 1);

        for (int b = 0; b < bands; b++)
        {
            float db = bandLevels[b];
            double y = RtaDbToY(db, mainH);
            double x = b * slotWidth + 1.5;
            double barHeight = Math.Max(0.0, mainH - y);

            var rect = new Rect(x, y, barWidth, barHeight);
            context.FillRectangle(barBrush, rect);
            context.DrawRectangle(barPen, rect);

            if (bands <= 10 || b % 3 == 0)
            {
                float fc = OctaveBandRtaCalculator.ThirdOctaveCenters[b];
                string labelStr = fc >= 1000f ? $"{fc / 1000f:0.#}k" : $"{fc:0}";
                var label = new FormattedText(
                    labelStr,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    LabelFont,
                    8,
                    new SolidColorBrush(TextColor));
                context.DrawText(label, new Point(x, mainH + 2));
            }
        }
    }

    private void DrawStoredRtaTrace(DrawingContext context, double w, double mainH, AcousticTrace trace)
    {
        Color traceColor = Color.Parse(trace.HexColor);
        var rtaPen = new Pen(new SolidColorBrush(traceColor), 1.6, DashStyle.Dash);
        var rtaGeometry = new StreamGeometry();
        bool rtaStarted = false;

        using (var rtaCtx = rtaGeometry.Open())
        {
            for (int i = 1; i < trace.Frequencies.Length; i++)
            {
                float freq = trace.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                double x = FreqToX(freq, w);
                double yRta = RtaDbToY(trace.MagnitudeDb[i] + trace.OffsetDb, mainH);

                if (!rtaStarted)
                {
                    rtaCtx.BeginFigure(new Point(x, yRta), false);
                    rtaStarted = true;
                }
                else
                {
                    rtaCtx.LineTo(new Point(x, yRta));
                }
            }
        }

        if (rtaStarted)
        {
            context.DrawGeometry(null, rtaPen, rtaGeometry);
        }
    }

    private void DrawStoredTrace(DrawingContext context, double w, double mainH, double cohTop, double cohH, AcousticTrace trace)
    {
        Color traceColor = Color.Parse(trace.HexColor);
        var magPen = new Pen(new SolidColorBrush(traceColor), 1.6, DashStyle.Dash);
        var phasePen = new Pen(new SolidColorBrush(Color.FromArgb(170, traceColor.R, traceColor.G, traceColor.B)), 1.2, DashStyle.Dot);
        var magGeometry = new StreamGeometry();
        var phaseGeometry = new StreamGeometry();
        bool magStarted = false;
        bool phaseStarted = false;

        using (var magCtx = magGeometry.Open())
        using (var phaseCtx = phaseGeometry.Open())
        {
            for (int i = 1; i < trace.Frequencies.Length; i++)
            {
                float freq = trace.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                trace.GetDisplayValues(i, out float mag, out float phase, out _);
                double x = FreqToX(freq, w);
                double yMag = DbToY(mag, mainH);
                double yPhase = PhaseToY(phase, mainH);

                if (!magStarted)
                {
                    magCtx.BeginFigure(new Point(x, yMag), false);
                    magStarted = true;
                }
                else
                {
                    magCtx.LineTo(new Point(x, yMag));
                }

                if (!phaseStarted)
                {
                    phaseCtx.BeginFigure(new Point(x, yPhase), false);
                    phaseStarted = true;
                }
                else
                {
                    phaseCtx.LineTo(new Point(x, yPhase));
                }
            }
        }

        if (magStarted)
        {
            context.DrawGeometry(null, magPen, magGeometry);
        }
        if (phaseStarted)
        {
            context.DrawGeometry(null, phasePen, phaseGeometry);
        }
    }

    private static uint DbToColorUint(float db)
    {
        float norm = Math.Clamp((db + 84f) / 84f, 0f, 1f);

        byte r, g, b;
        if (norm < 0.25f)
        {
            float t = norm / 0.25f;
            r = (byte)(15 + t * 40);
            g = (byte)(15 + t * 20);
            b = (byte)(30 + t * 150);
        }
        else if (norm < 0.5f)
        {
            float t = (norm - 0.25f) / 0.25f;
            r = (byte)(55 - t * 45);
            g = (byte)(35 + t * 180);
            b = (byte)(180 + t * 50);
        }
        else if (norm < 0.75f)
        {
            float t = (norm - 0.5f) / 0.25f;
            r = (byte)(10 + t * 245);
            g = (byte)(215 + t * 40);
            b = (byte)(230 - t * 230);
        }
        else
        {
            float t = (norm - 0.75f) / 0.25f;
            r = 255;
            g = (byte)(255 - t * 180);
            b = (byte)(t * 60);
        }

        return 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static int FreqToBin(float targetFreq, float[] frequencies)
    {
        int low = 0;
        int high = frequencies.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (frequencies[mid] < targetFreq) low = mid + 1;
            else high = mid - 1;
        }
        return Math.Clamp(low, 0, frequencies.Length - 1);
    }

    private void DrawSpectrogram(DrawingContext context, double w, double h, SpectrogramBuffer buffer, float[] frequencies)
    {
        int frameCount = buffer.Count;
        if (frameCount == 0) return;

        const int bmpW = 240;
        int bmpH = 100;

        if (_spectrogramBmp == null || _spectrogramBmp.PixelSize.Width != bmpW || _spectrogramBmp.PixelSize.Height != bmpH)
        {
            _spectrogramBmp?.Dispose();
            _spectrogramBmp = new WriteableBitmap(new PixelSize(bmpW, bmpH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        using (var fb = _spectrogramBmp.Lock())
        {
            unsafe
            {
                uint* ptr = (uint*)fb.Address;
                int stride = fb.RowBytes / 4;

                for (int row = 0; row < frameCount; row++)
                {
                    var frame = buffer.GetFrame(row);
                    uint* rowPtr = ptr + (row * stride);

                    for (int col = 0; col < bmpW; col++)
                    {
                        float logF = MathF.Log10(20f) + (col / (float)bmpW) * (MathF.Log10(20000f) - MathF.Log10(20f));
                        float f = MathF.Pow(10f, logF);
                        int bin = FreqToBin(f, frequencies);
                        float db = bin < frame.Length ? frame[bin] : -96f;

                        rowPtr[col] = DbToColorUint(db);
                    }
                }
            }
        }

        context.DrawImage(_spectrogramBmp, new Rect(0, 0, w, h * 0.95));

        var label = new FormattedText(
            "Spectrogram / Waterfall (Time vs Frequency)",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            LabelFont,
            12,
            new SolidColorBrush(Color.Parse("#00E5FF")));
        context.DrawText(label, new Point(10, 10));
    }

    private void DrawTargetCurve(DrawingContext context, double w, double mainH, TargetCurve target)
    {
        var targetGeom = new StreamGeometry();
        bool started = false;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#FFC107")), 1.5, DashStyle.Dash);

        using (var ctx = targetGeom.Open())
        {
            const int steps = 120;
            for (int i = 0; i <= steps; i++)
            {
                float logF = MathF.Log10(20f) + (i / (float)steps) * (MathF.Log10(20000f) - MathF.Log10(20f));
                float f = MathF.Pow(10f, logF);
                float db = target.Evaluate(f);

                double x = FreqToX(f, w);
                double y = DbToY(db, mainH);

                if (!started)
                {
                    ctx.BeginFigure(new Point(x, y), false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(new Point(x, y));
                }
            }
        }

        if (started)
        {
            context.DrawGeometry(null, pen, targetGeom);
            var label = new FormattedText(
                $"Target: {target.Name}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                11,
                new SolidColorBrush(Color.Parse("#FFC107")));
            context.DrawText(label, new Point(w - 200, 10));
        }
    }

    private void DrawPeqPreview(DrawingContext context, double w, double mainH, MeasurementSnapshot snap, IReadOnlyList<PeqFilterSuggestion> filters)
    {
        int count = snap.Frequencies.Length;
        float[] peqResponse = new float[count];
        BiquadFilter.EvaluateCascade(filters, snap.Frequencies, peqResponse, snap.SampleRate);

        var peqGeom = new StreamGeometry();
        var simGeom = new StreamGeometry();
        bool peqStarted = false;
        bool simStarted = false;

        using (var peqCtx = peqGeom.Open())
        using (var simCtx = simGeom.Open())
        {
            for (int i = 1; i < count; i++)
            {
                float freq = snap.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                double x = FreqToX(freq, w);

                double yPeq = DbToY(peqResponse[i], mainH);
                if (!peqStarted)
                {
                    peqCtx.BeginFigure(new Point(x, yPeq), false);
                    peqStarted = true;
                }
                else
                {
                    peqCtx.LineTo(new Point(x, yPeq));
                }

                float correctedMag = snap.MagnitudeDb[i] + peqResponse[i];
                double ySim = DbToY(correctedMag, mainH);
                if (!simStarted)
                {
                    simCtx.BeginFigure(new Point(x, ySim), false);
                    simStarted = true;
                }
                else
                {
                    simCtx.LineTo(new Point(x, ySim));
                }
            }
        }

        if (peqStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#40C4FF")), 1.5, DashStyle.Dash), peqGeom);
        }
        if (simStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#76FF03")), 2.2), simGeom);

            var label = new FormattedText(
                "Simulated Corrected Response (PEQ Preview)",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                11,
                new SolidColorBrush(Color.Parse("#76FF03")));
            context.DrawText(label, new Point(w - 280, 26));
        }
    }

    private void DrawDeltaCurve(DrawingContext context, double w, double mainH, MeasurementSnapshot snap, TargetCurve target)
    {
        var deltaGeom = new StreamGeometry();
        bool started = false;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#E040FB")), 2.0);

        using (var ctx = deltaGeom.Open())
        {
            for (int i = 1; i < snap.Frequencies.Length; i++)
            {
                float freq = snap.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                float measDb = snap.MagnitudeDb[i];
                float targetDb = target.Evaluate(freq);
                float deltaDb = measDb - targetDb;

                double x = FreqToX(freq, w);
                double y = DbToY(deltaDb, mainH);

                if (!started)
                {
                    ctx.BeginFigure(new Point(x, y), false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(new Point(x, y));
                }
            }
        }

        if (started)
        {
            context.DrawGeometry(null, pen, deltaGeom);
            var label = new FormattedText(
                "Δ Delta (Meas - Target)",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                11,
                new SolidColorBrush(Color.Parse("#E040FB")));
            context.DrawText(label, new Point(w - 360, 10));
        }
    }

    private void DrawFrequencyGrid(DrawingContext context, double w, double mainH, double cohTop, double cohH)
    {
        float[] majorFreqs = [20f, 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f];
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);

        foreach (float f in majorFreqs)
        {
            double x = FreqToX(f, w);
            if (x < 0 || x > w) continue;

            context.DrawLine(gridPen, new Point(x, 0), new Point(x, mainH + cohH));

            string text = f >= 1000f ? $"{f / 1000f:0.#}k" : $"{f:0}";
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                11,
                new SolidColorBrush(TextColor));

            context.DrawText(formattedText, new Point(x + 3, cohTop - 15));
        }
    }

    private void DrawMagnitudeAndPhaseGrid(DrawingContext context, double w, double mainH)
    {
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1, DashStyle.Dash);

        for (float db = -36f; db <= 18f; db += 6f)
        {
            double y = DbToY(db, mainH);
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));

            var label = new FormattedText(
                $"{db:+0;-0;0} dB",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                10,
                new SolidColorBrush(Color.Parse("#00E5FF")));
            context.DrawText(label, new Point(5, y - 12));
        }

        float[] phaseTicks = [180f, 90f, 0f, -90f, -180f];
        foreach (float deg in phaseTicks)
        {
            double y = PhaseToY(deg, mainH);
            var label = new FormattedText(
                $"{deg:+0;-0;0}°",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                10,
                new SolidColorBrush(Color.Parse("#FF9100")));
            context.DrawText(label, new Point(w - 35, y - 12));
        }
    }

    private void DrawCoherenceGrid(DrawingContext context, double w, double cohTop, double cohH)
    {
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#2C3440")), 1.5), new Point(0, cohTop), new Point(w, cohTop));

        double y100 = cohTop;
        double y50 = cohTop + cohH * 0.5;

        context.DrawLine(gridPen, new Point(0, y50), new Point(w, y50));

        var label100 = new FormattedText("1.0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 9, new SolidColorBrush(CohLineColor));
        context.DrawText(label100, new Point(5, y100 + 2));

        var label50 = new FormattedText("0.5", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 9, new SolidColorBrush(TextColor));
        context.DrawText(label50, new Point(5, y50 - 10));

        if (CoherenceThreshold > 0.01f)
        {
            double yThresh = cohTop + cohH * (1.0 - CoherenceThreshold);
            var threshPen = new Pen(new SolidColorBrush(Color.Parse("#D32F2F")), 1.2, DashStyle.Dash);
            context.DrawLine(threshPen, new Point(0, yThresh), new Point(w, yThresh));

            var labelThresh = new FormattedText(
                $"Cutoff: {CoherenceThreshold * 100:0}%",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                9,
                new SolidColorBrush(Color.Parse("#FF5252")));
            context.DrawText(labelThresh, new Point(w - 75, yThresh - 12));
        }
    }

    private void DrawDataCurves(DrawingContext context, double w, double mainH, double cohTop, double cohH, MeasurementSnapshot snap)
    {
        var magGeometry = new StreamGeometry();
        var phaseGeometry = new StreamGeometry();
        var cohGeometry = new StreamGeometry();

        int count = snap.Frequencies.Length;
        bool magStarted = false;
        bool phaseStarted = false;
        bool cohStarted = false;

        float[]? gdArray = null;
        if (ShowGroupDelay && snap.BinCount > 0)
        {
            gdArray = new float[snap.BinCount];
            GroupDelayCalculator.CalculateGroupDelayMs(snap.Frequencies, snap.PhaseDegrees, gdArray);
        }

        MinimumPhaseResult? minPhaseResult = null;
        var minPhaseGeometry = new StreamGeometry();
        bool minPhaseStarted = false;
        if (ShowMinimumPhase && snap.BinCount > 0)
        {
            minPhaseResult = MinimumPhaseAnalyzer.Analyze(snap.Frequencies, snap.MagnitudeDb, snap.PhaseDegrees);
        }

        using (var magCtx = magGeometry.Open())
        using (var phaseCtx = phaseGeometry.Open())
        using (var cohCtx = cohGeometry.Open())
        {
            for (int i = 1; i < count; i++)
            {
                float freq = snap.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                double x = FreqToX(freq, w);

                // Magnitud
                double yMag = DbToY(snap.MagnitudeDb[i], mainH);
                if (!magStarted)
                {
                    magCtx.BeginFigure(new Point(x, yMag), false);
                    magStarted = true;
                }
                else
                {
                    magCtx.LineTo(new Point(x, yMag));
                }

                // Coherencia
                float coh = Math.Clamp(snap.Coherence[i], 0f, 1f);
                double yCoh = cohTop + cohH * (1.0 - coh);
                if (!cohStarted)
                {
                    cohCtx.BeginFigure(new Point(x, yCoh), false);
                    cohStarted = true;
                }
                else
                {
                    cohCtx.LineTo(new Point(x, yCoh));
                }

                // Fase con Coherence Blanking
                if (CoherenceThreshold > 0.001f && coh < CoherenceThreshold)
                {
                    phaseStarted = false;
                }
                else
                {
                    double yPhase = ShowGroupDelay && gdArray != null
                        ? GroupDelayToY(gdArray[i], mainH)
                        : PhaseToY(snap.PhaseDegrees[i], mainH);
                    if (!phaseStarted)
                    {
                        phaseCtx.BeginFigure(new Point(x, yPhase), false);
                        phaseStarted = true;
                    }
                    else
                    {
                        phaseCtx.LineTo(new Point(x, yPhase));
                    }
                }

                if (minPhaseResult != null)
                {
                    double yMp = PhaseToY(minPhaseResult.MinPhaseDegrees[i], mainH);
                    if (!minPhaseStarted)
                    {
                        using var mpCtx = minPhaseGeometry.Open();
                        mpCtx.BeginFigure(new Point(x, yMp), false);
                        minPhaseStarted = true;
                    }
                }
            }
        }

        if (minPhaseResult != null)
        {
            using (var mpCtx = minPhaseGeometry.Open())
            {
                bool started = false;
                for (int i = 1; i < count; i++)
                {
                    float freq = snap.Frequencies[i];
                    if (freq < 20f || freq > 20000f) continue;
                    double x = FreqToX(freq, w);
                    double yMp = PhaseToY(minPhaseResult.MinPhaseDegrees[i], mainH);
                    if (!started) { mpCtx.BeginFigure(new Point(x, yMp), false); started = true; }
                    else { mpCtx.LineTo(new Point(x, yMp)); }
                }
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#76FF03")), 1.5, DashStyle.Dash), minPhaseGeometry);
            var mpLabel = new FormattedText("Min Phase (---)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 10, new SolidColorBrush(Color.Parse("#76FF03")));
            context.DrawText(mpLabel, new Point(w - 200, 26));
        }

        if (magStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(MagLineColor), 2.2), magGeometry);
        }
        if (phaseStarted)
        {
            Color pColor = ShowGroupDelay ? Color.Parse("#FFB300") : PhaseLineColor;
            context.DrawGeometry(null, new Pen(new SolidColorBrush(pColor), 1.6), phaseGeometry);
        }
        if (cohStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(CohLineColor), 1.8), cohGeometry);
        }
    }

    private void DrawCrosshairAndReadout(DrawingContext context, double w, double mainH, double cohTop, double cohH, Point mouse, MeasurementSnapshot snap, bool isRta)
    {
        var crossPen = new Pen(new SolidColorBrush(CrosshairColor), 1, DashStyle.Dash);
        context.DrawLine(crossPen, new Point(mouse.X, 0), new Point(mouse.X, mainH + cohH));
        context.DrawLine(crossPen, new Point(0, mouse.Y), new Point(w, mouse.Y));

        float mouseFreq = XToFreq(mouse.X, w);
        int closestBin = Math.Clamp((int)Math.Round(mouseFreq / (snap.SampleRate / snap.FftSize)), 0, snap.Frequencies.Length - 1);

        float fVal = snap.Frequencies[closestBin];

        string readout;
        if (isRta)
        {
            float rtaVal = snap.MagnitudeDb[closestBin];
            float maxVal = snap.RtaMaxHoldDb[closestBin];
            readout = $"{fVal:0.#} Hz | RTA: {rtaVal:+0.00;-0.00;0.00} dBFS | Max Hold: {maxVal:+0.00;-0.00;0.00} dBFS";
        }
        else
        {
            float magVal = snap.MagnitudeDb[closestBin];
            float cohVal = snap.Coherence[closestBin];
            if (ShowGroupDelay)
            {
                float[] gdArr = new float[snap.BinCount];
                GroupDelayCalculator.CalculateGroupDelayMs(snap.Frequencies, snap.PhaseDegrees, gdArr);
                float gdVal = gdArr[closestBin];
                readout = $"{fVal:0.#} Hz | Mag: {magVal:+0.00;-0.00;0.00} dB | GD: {gdVal:+0.00;-0.00;0.00} ms | Coh: {cohVal * 100f:0.0}%";
            }
            else
            {
                float phaseVal = snap.PhaseDegrees[closestBin];
                readout = $"{fVal:0.#} Hz | Mag: {magVal:+0.00;-0.00;0.00} dB | Phase: {phaseVal:+0.0;-0.0;0.0}Â° | Coh: {cohVal * 100f:0.0}%";
            }
        }

        var readoutText = new FormattedText(readout, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 13, new SolidColorBrush(Color.Parse("#E0E6ED")));

        var badgeRect = new Rect(w / 2 - 180, 10, 360, 26);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#CC1A202C")), badgeRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#3A4556")), 1), badgeRect, 4);
        context.DrawText(readoutText, new Point(w / 2 - 170, 14));
    }

    private static double FreqToX(float freq, double w)
    {
        double minLog = Math.Log10(20.0);
        double maxLog = Math.Log10(20000.0);
        double fLog = Math.Log10(Math.Clamp(freq, 20f, 20000f));
        return ((fLog - minLog) / (maxLog - minLog)) * w;
    }

    private static float XToFreq(double x, double w)
    {
        double minLog = Math.Log10(20.0);
        double maxLog = Math.Log10(20000.0);
        double fLog = minLog + (x / w) * (maxLog - minLog);
        return (float)Math.Pow(10.0, fLog);
    }

    private static double DbToY(float db, double mainH)
    {
        const float maxDb = 18f;
        const float minDb = -36f;
        float norm = (Math.Clamp(db, minDb, maxDb) - minDb) / (maxDb - minDb);
        return mainH * (1.0 - norm);
    }

    private static double RtaDbToY(float db, double mainH)
    {
        const float maxDb = 0f;
        const float minDb = -96f;
        float norm = (Math.Clamp(db, minDb, maxDb) - minDb) / (maxDb - minDb);
        return mainH * (1.0 - norm);
    }

    private static double PhaseToY(float deg, double mainH)
    {
        const float maxDeg = 180f;
        const float minDeg = -180f;
        float norm = (Math.Clamp(deg, minDeg, maxDeg) - minDeg) / (maxDeg - minDeg);
        return mainH * (1.0 - norm);
    }

    private static double GroupDelayToY(float ms, double mainH)
    {
        float clamped = Math.Clamp(ms, -10f, 30f);
        return mainH * (1.0 - (clamped + 10f) / 40f);
    }

    private static double EtcDbToY(float db, double h)
    {
        const float maxDb = 0f;
        const float minDb = -80f;
        float norm = (Math.Clamp(db, minDb, maxDb) - minDb) / (maxDb - minDb);
        return (h * 0.9) * (1.0 - norm) + (h * 0.05);
    }

    private void DrawImpulseAndEtc(DrawingContext context, double w, double h, MeasurementSnapshot snapshot)
    {
        var etc = EtcCalculator.Calculate(snapshot.ImpulseResponse, (int)snapshot.SampleRate, minDb: -80f, reflectionThresholdDb: -30f);
        if (etc.TimeMs.Length == 0) return;

        float maxTimeMs = Math.Min(60.0f, etc.TimeMs[^1]);
        if (maxTimeMs <= 0f) maxTimeMs = 50.0f;

        var gridPen = new Pen(new SolidColorBrush(GridColor), 1, DashStyle.Dash);
        var textBrush = new SolidColorBrush(TextColor);

        // 1. Grid temporal (cada 5 ms)
        for (float t = 0f; t <= maxTimeMs; t += 5f)
        {
            double x = (t / maxTimeMs) * w;
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, h));

            var lbl = new FormattedText($"{t:0} ms", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 10, textBrush);
            context.DrawText(lbl, new Point(x + 3, h - 16));
        }

        // 2. Grid de amplitud dB (0 dB a -80 dB, paso 10 dB)
        for (float db = -80f; db <= 0f; db += 10f)
        {
            double y = EtcDbToY(db, h);
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));

            var lbl = new FormattedText($"{db:0} dB", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 10, textBrush);
            context.DrawText(lbl, new Point(8, y - 6));
        }

        // 3. Curva de respuesta impulsional lineal normalizada (h(t)) en fondo atenuado
        float maxIr = 1e-12f;
        for (int i = 0; i < snapshot.ImpulseResponse.Length; i++)
        {
            float abs = MathF.Abs(snapshot.ImpulseResponse[i]);
            if (abs > maxIr) maxIr = abs;
        }

        var irGeom = new StreamGeometry();
        using (var sgc = irGeom.Open())
        {
            bool first = true;
            double baselineY = EtcDbToY(-80f, h);
            for (int i = 0; i < snapshot.ImpulseResponse.Length; i++)
            {
                float t = etc.TimeMs[i];
                if (t > maxTimeMs) break;
                double x = (t / maxTimeMs) * w;
                float normIr = snapshot.ImpulseResponse[i] / maxIr;
                double y = baselineY - (Math.Max(0f, normIr) * (h * 0.4));
                if (first) { sgc.BeginFigure(new Point(x, y), false); first = false; }
                else { sgc.LineTo(new Point(x, y)); }
            }
        }
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#334155")), 1), irGeom);

        // 4. Curva de EnergÃ­a-Tiempo (ETC dB) en Cyan brillante
        var etcGeom = new StreamGeometry();
        using (var sgc = etcGeom.Open())
        {
            bool first = true;
            for (int i = 0; i < etc.TimeMs.Length; i++)
            {
                float t = etc.TimeMs[i];
                if (t > maxTimeMs) break;
                double x = (t / maxTimeMs) * w;
                double y = EtcDbToY(etc.EnvelopeDb[i], h);
                if (first) { sgc.BeginFigure(new Point(x, y), false); first = false; }
                else { sgc.LineTo(new Point(x, y)); }
            }
        }
        context.DrawGeometry(null, new Pen(new SolidColorBrush(MagLineColor), 2), etcGeom);

        // 5. Marcador de Sonido Directo
        double directX = (etc.DirectSoundTimeMs / maxTimeMs) * w;
        context.DrawLine(new Pen(new SolidColorBrush(CohLineColor), 1.5, DashStyle.Dash), new Point(directX, 0), new Point(directX, h));
        var directLbl = new FormattedText($"DIRECT ({etc.DirectSoundTimeMs:0.0}ms)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 10, new SolidColorBrush(CohLineColor));
        context.DrawText(directLbl, new Point(directX + 4, 18));

        // 6. Marcadores de Reflexiones Tempranas
        var reflPen = new Pen(new SolidColorBrush(Color.Parse("#FFD600")), 1, DashStyle.Dash);
        for (int i = 0; i < etc.Reflections.Count; i++)
        {
            var r = etc.Reflections[i];
            if (r.TimeMs > maxTimeMs) continue;
            double rx = (r.TimeMs / maxTimeMs) * w;
            double ry = EtcDbToY(r.LevelDb, h);

            context.DrawLine(reflPen, new Point(rx, ry), new Point(rx, h));
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#FFD600")), null, new Point(rx, ry), 3.5, 3.5);

            var rLbl = new FormattedText($"R{i + 1}: +{r.RelativeDelayMs:0.0}ms ({r.LevelDb:0.0}dB, +{r.PathDifferenceMeters:0.00}m)",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 9, new SolidColorBrush(Color.Parse("#FFD600")));
            context.DrawText(rLbl, new Point(rx + 4, ry - 14));
        }
    }

    private void DrawEtcCrosshair(DrawingContext context, double w, double h, Point mouse, MeasurementSnapshot snapshot)
    {
        var crosshairPen = new Pen(new SolidColorBrush(CrosshairColor), 1, DashStyle.Dash);
        context.DrawLine(crosshairPen, new Point(mouse.X, 0), new Point(mouse.X, h));
        context.DrawLine(crosshairPen, new Point(0, mouse.Y), new Point(w, mouse.Y));

        float maxTimeMs = 60.0f;
        float curTimeMs = (float)(mouse.X / w) * maxTimeMs;
        float normY = (float)((mouse.Y - (h * 0.05)) / (h * 0.9));
        float curDb = Math.Clamp(-normY * 80.0f, -80f, 0f);
        float distM = curTimeMs * 0.001f * 343.0f;

        string readout = $"Time: {curTimeMs:0.00} ms  |  ETC: {curDb:0.0} dB  |  Path Dist: {distM:0.00} m";
        var readoutText = new FormattedText(readout, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 12, new SolidColorBrush(Color.Parse("#E0E6ED")));

        var badgeRect = new Rect(w / 2 - 160, 10, 320, 26);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#CC1A202C")), badgeRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#3A4556")), 1), badgeRect, 4);
        context.DrawText(readoutText, new Point(w / 2 - 150, 14));
    }
}
