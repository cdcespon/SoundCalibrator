using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SoundCalibrator.Audio.Devices;
using SoundCalibrator.Audio.Engine;
using SoundCalibrator.Audio.Generators;
using SoundCalibrator.Core.Averaging;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Smoothing;
using Avalonia.Platform.Storage;
using SoundCalibrator.Core.Calibration;
using SoundCalibrator.Core.Serialization;

namespace SoundCalibrator.App;

public partial class MainWindow : Window
{
    private readonly AcousticMeasurementEngine _engine;
    private SyntheticAudioGenerator? _syntheticGen;
    private IDisposable? _wasapiDevice;
    private bool _isPaused;
    private float _lastDetectedDelayMs;
    private bool _isAligned;
    private MeasurementSnapshot? _lastSnapshot;

    private static readonly string[] TraceColors = ["#E040FB", "#76FF03", "#FFD600", "#FF4081", "#00E676", "#448AFF"];

    public MainWindow()
    {
        InitializeComponent();

        _engine = new AcousticMeasurementEngine(1024);
        _syntheticGen = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 512);

        _engine.AttachDevice(_syntheticGen);
        _engine.SnapshotReady += OnSnapshotReady;

        SetupEventHandlers();
        _engine.Start();
    }

    private void SetupEventHandlers()
    {
        StartStopBtn.Click += OnStartStopClick;
        ResetBtn.Click += (s, e) =>
        {
            _engine.Reset();
            _engine.DelayCompensationMs = 0f;
            _isAligned = false;
            AutoAlignBtn.Content = "ALIGN";
            AutoAlignBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#E65100");
        };

        AutoAlignBtn.Click += (s, e) =>
        {
            if (!_isAligned)
            {
                _engine.DelayCompensationMs = -_lastDetectedDelayMs;
                _isAligned = true;
                AutoAlignBtn.Content = "UNALIGN";
                AutoAlignBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#388E3C");
            }
            else
            {
                _engine.DelayCompensationMs = 0f;
                _isAligned = false;
                AutoAlignBtn.Content = "ALIGN";
                AutoAlignBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#E65100");
            }
        };

        CaptureTraceBtn.Click += (s, e) =>
        {
            if (_lastSnapshot != null)
            {
                int idx = GraphControl.StoredTraces.Count;
                string color = TraceColors[idx % TraceColors.Length];
                string name = $"Trace {idx + 1}";
                var trace = new AcousticTrace(name, color, _lastSnapshot.Frequencies, _lastSnapshot.MagnitudeDb, _lastSnapshot.PhaseDegrees, _lastSnapshot.Coherence);
                GraphControl.StoredTraces.Add(trace);
                TracesCountText.Text = $"Captured Traces: {GraphControl.StoredTraces.Count}";
                GraphControl.InvalidateVisual();
            }
        };

        ClearTracesBtn.Click += (s, e) =>
        {
            GraphControl.StoredTraces.Clear();
            TracesCountText.Text = "Captured Traces: 0";
            GraphControl.InvalidateVisual();
        };

        DelaySlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value) && _syntheticGen != null)
            {
                float val = (float)DelaySlider.Value;
                _syntheticGen.DelayMs = val;
                DelayText.Text = $"{val:0.0} ms";
            }
        };

        InvertPolarityBtn.Click += (s, e) =>
        {
            _engine.InvertPolarity = !_engine.InvertPolarity;
            InvertPolarityBtn.Background = _engine.InvertPolarity
                ? Avalonia.Media.SolidColorBrush.Parse("#D32F2F")
                : Avalonia.Media.SolidColorBrush.Parse("#263238");
            InvertPolarityBtn.Foreground = _engine.InvertPolarity
                ? Avalonia.Media.SolidColorBrush.Parse("#FFFFFF")
                : Avalonia.Media.SolidColorBrush.Parse("#B0BEC5");
        };

        ModeCombo.SelectionChanged += OnModeChanged;
        SourceCombo.SelectionChanged += OnSourceChanged;
        FftCombo.SelectionChanged += OnFftOrWindowChanged;
        WindowCombo.SelectionChanged += OnFftOrWindowChanged;
        AveragingCombo.SelectionChanged += OnAveragingChanged;
        SmoothingCombo.SelectionChanged += OnSmoothingChanged;
        BlankingCombo.SelectionChanged += OnBlankingChanged;

        DeltaBtn.Click += (s, e) =>
        {
            GraphControl.ShowDeltaCurve = !GraphControl.ShowDeltaCurve;
            DeltaBtn.Background = GraphControl.ShowDeltaCurve
                ? Avalonia.Media.SolidColorBrush.Parse("#8E24AA")
                : Avalonia.Media.SolidColorBrush.Parse("#263238");
            DeltaBtn.Foreground = GraphControl.ShowDeltaCurve
                ? Avalonia.Media.SolidColorBrush.Parse("#FFFFFF")
                : Avalonia.Media.SolidColorBrush.Parse("#B0BEC5");
            GraphControl.InvalidateVisual();
        };

        TargetCombo.SelectionChanged += (s, e) =>
        {
            var preset = TargetCombo.SelectedIndex switch
            {
                1 => TargetCurvePreset.HarmanTarget,
                2 => TargetCurvePreset.BruelKjaer1974,
                3 => TargetCurvePreset.CinemaXCurve,
                4 => TargetCurvePreset.Flat,
                _ => TargetCurvePreset.None
            };
            GraphControl.ActiveTargetCurve = preset == TargetCurvePreset.None ? null : TargetCurve.CreatePreset(preset);
            GraphControl.InvalidateVisual();
        };

        ExportTraceBtn.Click += async (s, e) =>
        {
            if (GraphControl.StoredTraces.Count == 0 && _lastSnapshot == null) return;

            var traceToExport = GraphControl.StoredTraces.Count > 0
                ? GraphControl.StoredTraces[^1]
                : new AcousticTrace("Measurement", "#00E5FF", _lastSnapshot!.Frequencies, _lastSnapshot.MagnitudeDb, _lastSnapshot.PhaseDegrees, _lastSnapshot.Coherence);

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export Acoustic Trace (CSV)",
                    DefaultExtension = "csv",
                    SuggestedFileName = $"{traceToExport.Name.Replace(' ', '_')}.csv"
                });

                if (file != null)
                {
                    string csv = TraceSerializer.ExportToCsv(traceToExport);
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new System.IO.StreamWriter(stream);
                    await writer.WriteAsync(csv);
                }
            }
        };

        ImportTraceBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Trace (.csv) or Mic Calibration (.cal, .txt)",
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    var file = files[0];
                    await using var stream = await file.OpenReadAsync();
                    using var reader = new System.IO.StreamReader(stream);
                    string text = await reader.ReadToEndAsync();

                    if (file.Name.EndsWith(".cal", System.StringComparison.OrdinalIgnoreCase) ||
                        file.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _engine.Calibration.LoadFromText(text);
                        StatusDeviceText.Text = $"Cal Loaded: {file.Name} ({_engine.Calibration.Points.Count} pts)";
                    }
                    else
                    {
                        try
                        {
                            var trace = TraceSerializer.ImportFromCsv(text);
                            GraphControl.StoredTraces.Add(trace);
                            TracesCountText.Text = $"Captured Traces: {GraphControl.StoredTraces.Count}";
                            GraphControl.InvalidateVisual();
                        }
                        catch
                        {
                            // Formato no reconocido
                        }
                    }
                }
            }
        };
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Avalonia.Input.Key.Space:
                OnStartStopClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Avalonia.Input.Key.C:
                CaptureTraceBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.R:
                ResetBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.A:
                if (AutoAlignBtn.IsVisible)
                {
                    AutoAlignBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                break;
            case Avalonia.Input.Key.M:
                ModeCombo.SelectedIndex = (ModeCombo.SelectedIndex + 1) % ModeCombo.ItemCount;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.P:
                InvertPolarityBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D:
                DeltaBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D1 or Avalonia.Input.Key.NumPad1:
                SmoothingCombo.SelectedIndex = 0;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D2 or Avalonia.Input.Key.NumPad2:
                SmoothingCombo.SelectedIndex = 1;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D3 or Avalonia.Input.Key.NumPad3:
                SmoothingCombo.SelectedIndex = 2;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D4 or Avalonia.Input.Key.NumPad4:
                SmoothingCombo.SelectedIndex = 3;
                e.Handled = true;
                break;
        }
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        int mode = ModeCombo.SelectedIndex;
        bool isTf = mode == 0;
        bool isRta = mode == 1;
        bool isSpectro = mode == 2;

        _engine.IsRtaMode = !isTf;
        GraphControl.IsSpectrogramMode = isSpectro;

        BlankingPanel.IsVisible = isTf;
        DelayPanel.IsVisible = isTf;
        AutoDelayBorder.IsVisible = isTf;
        DeltaBtn.IsVisible = isTf;
        InvertPolarityBtn.IsVisible = isTf;

        GraphControl.InvalidateVisual();
    }

    private void OnFftOrWindowChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_engine == null || FftCombo == null || WindowCombo == null) return;

        int fftSize = FftCombo.SelectedIndex switch
        {
            0 => 512,
            1 => 1024,
            2 => 2048,
            3 => 4096,
            _ => 1024
        };

        WindowType winType = WindowCombo.SelectedIndex switch
        {
            0 => WindowType.Hann,
            1 => WindowType.BlackmanHarris,
            2 => WindowType.Rectangular,
            _ => WindowType.Hann
        };

        _engine.ReconfigureFft(fftSize, winType);
        UpdateStatusDeviceText();
    }

    private void OnSnapshotReady(MeasurementSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _lastDetectedDelayMs = snapshot.Delay.DelayMs;

        Dispatcher.UIThread.Post(() =>
        {
            GraphControl.UpdateSnapshot(snapshot);
            StatusAvgText.Text = $"Averages: {snapshot.AverageCount}";
            DetectedDelayText.Text = $"{snapshot.Delay.DelayMs:0.00} ms";
        });
    }

    private void OnStartStopClick(object? sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            _engine.Start();
            StartStopBtn.Content = "PAUSE";
            StartStopBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#1E88E5");
            _isPaused = false;
        }
        else
        {
            _engine.Stop();
            StartStopBtn.Content = "RESUME";
            StartStopBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#2E7D32");
            _isPaused = true;
        }
    }

    private void OnSourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        int idx = SourceCombo.SelectedIndex;
        if (idx == 0) // Pink noise
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.PinkNoise;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 1) // Sine 1k
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SineWave;
            _syntheticGen.SineFrequency = 1000f;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 2) // Sine Sweep
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SineSweep;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 3) // WASAPI
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var dev = WasapiAudioCaptureDevice.GetDefaultRecordingDevice();
                    if (dev != null)
                    {
                        var wasapi = new WasapiAudioCaptureDevice(dev);
                        _wasapiDevice = wasapi;
                        _engine.AttachDevice(wasapi);
                        StatusDeviceText.Text = $"WASAPI: {dev.FriendlyName} ({wasapi.SampleRate} Hz, FFT: {_engine.FftSize})";
                        if (!_isPaused) _engine.Start();
                    }
                    else
                    {
                        StatusDeviceText.Text = "WASAPI: No default recording device found";
                    }
                }
                catch (Exception ex)
                {
                    StatusDeviceText.Text = $"WASAPI Error: {ex.Message}";
                }
            }
            else
            {
                StatusDeviceText.Text = "WASAPI is supported only on Windows";
            }
        }
    }

    private void UpdateStatusDeviceText()
    {
        string srcName = SourceCombo.SelectedIndex switch
        {
            0 => "Pink Noise",
            1 => "Sine 1 kHz",
            2 => "Sine Sweep 20Hz-20kHz",
            _ => "Audio Device"
        };
        StatusDeviceText.Text = $"Synthetic: {srcName} ({_engine.SampleRate} Hz, FFT: {_engine.FftSize})";
    }

    private void OnAveragingChanged(object? sender, SelectionChangedEventArgs e)
    {
        _engine.Averaging = AveragingCombo.SelectedIndex switch
        {
            0 => AveragingType.None,
            1 => AveragingType.ExponentialFast,
            2 => AveragingType.ExponentialSlow,
            3 => AveragingType.Linear16,
            4 => AveragingType.Infinite,
            _ => AveragingType.ExponentialFast
        };
    }

    private void OnSmoothingChanged(object? sender, SelectionChangedEventArgs e)
    {
        _engine.Smoothing = SmoothingCombo.SelectedIndex switch
        {
            0 => OctaveSmoothingType.None,
            1 => OctaveSmoothingType.Octave1_3,
            2 => OctaveSmoothingType.Octave1_6,
            3 => OctaveSmoothingType.Octave1_12,
            4 => OctaveSmoothingType.Octave1_24,
            _ => OctaveSmoothingType.None
        };
    }

    private void OnBlankingChanged(object? sender, SelectionChangedEventArgs e)
    {
        GraphControl.CoherenceThreshold = BlankingCombo.SelectedIndex switch
        {
            0 => 0.0f,
            1 => 0.3f,
            2 => 0.5f,
            3 => 0.7f,
            _ => 0.0f
        };
        GraphControl.InvalidateVisual();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _engine.Dispose();
        _syntheticGen?.Dispose();
        _wasapiDevice?.Dispose();
    }
}
