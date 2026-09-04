using System;
using System.Threading.Tasks;
using SoundCalibrator.Audio.Generators;
using SoundCalibrator.Audio.Interfaces;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SyntheticAudioGeneratorTests
{
    [Fact]
    public async Task Generator_ProducesBlocksWithExpectedGainAndDelay()
    {
        using var generator = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 256);
        generator.SignalType = TestSignalType.SineWave;
        generator.SineFrequency = 1000f;
        generator.GainDb = 6.02f;
        generator.DelayMs = 0f;

        var tcs = new TaskCompletionSource<AudioBlockEventArgs>();

        generator.AudioBlockAvailable += (s, e) =>
        {
            tcs.TrySetResult(e);
        };

        generator.Start();

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(500));
        Assert.True(completedTask == tcs.Task, "Audio block was not received in time");

        var block = await tcs.Task;
        Assert.Equal(256, block.SampleCount);

        float maxRef = 0f;
        float maxMeas = 0f;
        for (int i = 0; i < block.SampleCount; i++)
        {
            maxRef = Math.Max(maxRef, Math.Abs(block.Reference[i]));
            maxMeas = Math.Max(maxMeas, Math.Abs(block.Measurement[i]));
        }

        Assert.True(maxRef > 0.1f, "Reference signal is silent");
        Assert.True(maxMeas > maxRef, "Measurement signal did not apply gain");
    }

    [Fact]
    public async Task GatedPinkNoise_ProducesSignalWithActiveAndMutedPhases()
    {
        using var generator = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 512);
        generator.SignalType = TestSignalType.GatedPinkNoise;
        generator.GateOnMs = 50f;
        generator.GateOffMs = 50f;

        var tcs = new TaskCompletionSource<AudioBlockEventArgs>();
        generator.AudioBlockAvailable += (s, e) => tcs.TrySetResult(e);
        generator.Start();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(500));
        Assert.True(completed == tcs.Task);
        var block = await tcs.Task;
        Assert.Equal(512, block.SampleCount);
    }

    [Fact]
    public async Task IecNoise_ProducesFilteredProgramSignal()
    {
        using var generator = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 512);
        generator.SignalType = TestSignalType.IecNoise;

        var tcs = new TaskCompletionSource<AudioBlockEventArgs>();
        generator.AudioBlockAvailable += (s, e) => tcs.TrySetResult(e);
        generator.Start();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(500));
        Assert.True(completed == tcs.Task);
        var block = await tcs.Task;
        Assert.Equal(512, block.SampleCount);
    }
}
