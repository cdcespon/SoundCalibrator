using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SoundCalibrator.Audio.Interfaces;

namespace SoundCalibrator.Audio.Devices;

[SupportedOSPlatform("windows")]
public sealed class WasapiAudioCaptureDevice : IAudioCaptureDevice
{
    private WasapiCapture? _capture;
    private readonly MMDevice _device;
    private bool _isRunning;
    private bool _disposed;

    public string DeviceName => _device.FriendlyName;
    public int SampleRate => _capture?.WaveFormat.SampleRate ?? 48000;
    public int Channels => _capture?.WaveFormat.Channels ?? 2;
    public bool IsRunning => _isRunning;

    public event EventHandler<AudioBlockEventArgs>? AudioBlockAvailable;

    public WasapiAudioCaptureDevice(MMDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public static IEnumerable<MMDevice> GetRecordingDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
    }

    public static MMDevice? GetDefaultRecordingDevice()
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    public void Start()
    {
        if (_isRunning) return;

        _capture = new WasapiCapture(_device);
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();
        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning || _capture == null) return;

        _isRunning = false;
        _capture.StopRecording();
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _capture = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isRunning || e.BytesRecorded == 0 || _capture == null) return;

        var format = _capture.WaveFormat;
        int channels = format.Channels;
        if (channels < 2)
        {
            var samples = ExtractChannelSamples(e.Buffer, e.BytesRecorded, format, 0, channels);
            AudioBlockAvailable?.Invoke(this, new AudioBlockEventArgs(samples, samples, samples.Length));
            return;
        }

        var refSamples = ExtractChannelSamples(e.Buffer, e.BytesRecorded, format, 0, channels);
        var measSamples = ExtractChannelSamples(e.Buffer, e.BytesRecorded, format, 1, channels);

        AudioBlockAvailable?.Invoke(this, new AudioBlockEventArgs(refSamples, measSamples, refSamples.Length));
    }

    private static float[] ExtractChannelSamples(byte[] buffer, int bytesRecorded, WaveFormat format, int targetChannel, int totalChannels)
    {
        int bytesPerSample = format.BitsPerSample / 8;
        int totalFrames = bytesRecorded / (bytesPerSample * totalChannels);
        var result = new float[totalFrames];

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (int i = 0; i < totalFrames; i++)
            {
                int offset = (i * totalChannels + targetChannel) * 4;
                result[i] = BitConverter.ToSingle(buffer, offset);
            }
        }
        else if (format.BitsPerSample == 16)
        {
            for (int i = 0; i < totalFrames; i++)
            {
                int offset = (i * totalChannels + targetChannel) * 2;
                short val = BitConverter.ToInt16(buffer, offset);
                result[i] = val / 32768f;
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _device.Dispose();
    }
}
