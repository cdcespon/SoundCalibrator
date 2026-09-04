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

        SourceCombo.SelectionChanged += OnSourceChanged;
        AveragingCombo.SelectionChanged += OnAveragingChanged;
        SmoothingCombo.SelectionChanged += OnSmoothingChanged;
        BlankingCombo.SelectionChanged += OnBlankingChanged;
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
            StatusDeviceText.Text = "Synthetic: Pink Noise (48 kHz, FFT: 1024)";
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 1) // Sine 1k
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SineWave;
            _syntheticGen.SineFrequency = 1000f;
            _engine.AttachDevice(_syntheticGen);
            StatusDeviceText.Text = "Synthetic: Sine 1 kHz (48 kHz, FFT: 1024)";
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 2) // Sine Sweep
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SineSweep;
            _engine.AttachDevice(_syntheticGen);
            StatusDeviceText.Text = "Synthetic: Sine Sweep 20Hz-20kHz (48 kHz, FFT: 1024)";
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
                        StatusDeviceText.Text = $"WASAPI: {dev.FriendlyName} ({wasapi.SampleRate} Hz, FFT: 1024)";
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
