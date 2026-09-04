using System;
using SoundCalibrator.Core.DSP;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SpectrogramBufferTests
{
    [Fact]
    public void PushFrame_StoresConsecutiveFramesInCircularOrder()
    {
        var buffer = new SpectrogramBuffer(capacity: 3, binCount: 4);

        float[] frame1 = [1f, 2f, 3f, 4f];
        float[] frame2 = [5f, 6f, 7f, 8f];
        float[] frame3 = [9f, 10f, 11f, 12f];

        buffer.PushFrame(frame1);
        buffer.PushFrame(frame2);
        buffer.PushFrame(frame3);

        Assert.Equal(3, buffer.Count);

        // Index 0 is newest (frame3)
        var newest = buffer.GetFrame(0);
        Assert.Equal(9f, newest[0]);
        Assert.Equal(12f, newest[3]);

        // Index 1 is frame2
        var middle = buffer.GetFrame(1);
        Assert.Equal(5f, middle[0]);

        // Index 2 is oldest (frame1)
        var oldest = buffer.GetFrame(2);
        Assert.Equal(1f, oldest[0]);
    }

    [Fact]
    public void PushFrame_OverwritesOldestWhenFull()
    {
        var buffer = new SpectrogramBuffer(capacity: 2, binCount: 2);

        buffer.PushFrame([10f, 20f]);
        buffer.PushFrame([30f, 40f]);
        buffer.PushFrame([50f, 60f]); // Overwrites [10f, 20f]

        Assert.Equal(2, buffer.Count);

        var newest = buffer.GetFrame(0);
        Assert.Equal(50f, newest[0]);

        var older = buffer.GetFrame(1);
        Assert.Equal(30f, older[0]);
    }

    [Fact]
    public void Clear_ResetsCountToZero()
    {
        var buffer = new SpectrogramBuffer(capacity: 5, binCount: 4);
        buffer.PushFrame([1f, 2f, 3f, 4f]);
        Assert.Equal(1, buffer.Count);

        buffer.Clear();
        Assert.Equal(0, buffer.Count);
    }
}
