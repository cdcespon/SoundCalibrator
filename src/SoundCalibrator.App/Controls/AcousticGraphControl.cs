using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SoundCalibrator.Audio.Engine;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.App.Controls;

public sealed class AcousticGraphControl : Control
{
    private MeasurementSnapshot? _currentSnapshot;
    private Point? _mousePosition;

    public List<AcousticTrace> StoredTraces { get; } = [];
    public float CoherenceThreshold { get; set; } = 0.0f;
    public TargetCurve? ActiveTargetCurve { get; set; }
    public bool ShowDeltaCurve { get; set; } = false;

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

        bool isRta = _currentSnapshot?.IsRtaMode ?? false;

        double mainH = isRta ? h * 0.95 : h * 0.75;
        double cohH = isRta ? 0 : h * 0.25;
        double cohTop = mainH;

        DrawFrequencyGrid(context, w, mainH, cohTop, cohH);

        if (isRta)
        {
            DrawRtaGrid(context, w, mainH);
            if (_currentSnapshot != null)
            {
                DrawRtaCurves(context, w, mainH, _currentSnapshot);
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

    private void DrawStoredTrace(DrawingContext context, double w, double mainH, double cohTop, double cohH, AcousticTrace trace)
    {
        Color traceColor = Color.Parse(trace.HexColor);
        var magPen = new Pen(new SolidColorBrush(traceColor), 1.6, DashStyle.Dash);
        var magGeometry = new StreamGeometry();
        bool started = false;

        using (var magCtx = magGeometry.Open())
        {
            for (int i = 1; i < trace.Frequencies.Length; i++)
            {
                float freq = trace.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                trace.GetDisplayValues(i, out float mag, out _, out _);
                double x = FreqToX(freq, w);
                double yMag = DbToY(mag, mainH);

                if (!started)
                {
                    magCtx.BeginFigure(new Point(x, yMag), false);
                    started = true;
                }
                else
                {
                    magCtx.LineTo(new Point(x, yMag));
                }
            }
        }

        if (started)
        {
            context.DrawGeometry(null, magPen, magGeometry);
        }
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
                    double yPhase = PhaseToY(snap.PhaseDegrees[i], mainH);
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
        }

        if (magStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(MagLineColor), 2.2), magGeometry);
        }
        if (phaseStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(PhaseLineColor), 1.6), phaseGeometry);
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
            float phaseVal = snap.PhaseDegrees[closestBin];
            float cohVal = snap.Coherence[closestBin];
            readout = $"{fVal:0.#} Hz | Mag: {magVal:+0.00;-0.00;0.00} dB | Phase: {phaseVal:+0.0;-0.0;0.0}° | Coh: {cohVal * 100f:0.0}%";
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
}
