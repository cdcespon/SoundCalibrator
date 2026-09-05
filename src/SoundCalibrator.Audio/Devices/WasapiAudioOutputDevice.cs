using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SoundCalibrator.Audio.Devices;

#pragma warning disable CS0618
[SupportedOSPlatform("windows")]
public sealed class WasapiAudioOutputDevice : IDisposable
{
    private WasapiOut? _wasapiOut;
    private readonly MMDevice _device;
    private bool _isRunning;
    private bool _disposed;
    private float _volume = 0.5f;
    private bool _isMuted = false;

    public string DeviceName => _device.FriendlyName;
    public bool IsRunning => _isRunning;

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public WasapiAudioOutputDevice(MMDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public static IEnumerable<MMDevice> GetPlaybackDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
    }

    public static MMDevice? GetDefaultPlaybackDevice()
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    public void Start(ISampleProvider sampleProvider)
    {
        if (_isRunning) return;

        var wrapped = new VolumeSampleProvider(sampleProvider, this);
        _wasapiOut = new WasapiOut(_device, AudioClientShareMode.Shared, useEventSync: true, latency: 100);
        _wasapiOut.Init(wrapped);
        _wasapiOut.Play();
        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning || _wasapiOut == null) return;
        _isRunning = false;
        _wasapiOut.Stop();
        _wasapiOut.Dispose();
        _wasapiOut = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private sealed class VolumeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly WasapiAudioOutputDevice _owner;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public VolumeSampleProvider(ISampleProvider source, WasapiAudioOutputDevice owner)
        {
            _source = source;
            _owner = owner;
        }

        public int Read(Span<float> buffer)
        {
            int read = _source.Read(buffer);
            if (_owner._isMuted || _owner._volume <= 0.0001f)
            {
                buffer[..read].Clear();
                return read;
            }

            float vol = _owner._volume;
            if (Math.Abs(vol - 1.0f) > 0.001f)
            {
                for (int i = 0; i < read; i++)
                {
                    buffer[i] *= vol;
                }
            }
            return read;
        }
    }
}
