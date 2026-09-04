using SoundCalibrator.Core.DSP;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class PhaseUnwrapperTests
{
    [Fact]
    public void UnwrapDegrees_RemovesDiscontinuitiesSmoothly()
    {
        // Fase con salto brusco de +170 a -170 (+340 de caída equivalente a rotar)
        float[] wrapped = [0f, 90f, 170f, -170f, -90f, 0f];
        float[] unwrapped = new float[wrapped.Length];

        PhaseUnwrapper.UnwrapDegrees(wrapped, unwrapped);

        // 170 -> -170 debería pasar a ser 190, luego 270, luego 360
        Assert.Equal(0f, unwrapped[0]);
        Assert.Equal(90f, unwrapped[1]);
        Assert.Equal(170f, unwrapped[2]);
        Assert.Equal(190f, unwrapped[3]);
        Assert.Equal(270f, unwrapped[4]);
        Assert.Equal(360f, unwrapped[5]);
    }
}
