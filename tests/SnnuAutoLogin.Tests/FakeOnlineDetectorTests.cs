using SnnuAutoLogin.Core;
using Xunit;

namespace SnnuAutoLogin.Tests;

/// <summary>假在线判定器测试：连续阈值、恢复重置、探测节奏。</summary>
public class FakeOnlineDetectorTests
{
    [Fact]
    public void 单次探测失败_证据不足()
    {
        var d = new FakeOnlineDetector(probeEveryNTicks: 2, strikeThreshold: 2);
        Assert.Equal(FakeOnlineVerdict.InsufficientEvidence, d.Record(false));
    }

    [Fact]
    public void 连续两次失败_判定假在线()
    {
        var d = new FakeOnlineDetector(2, 2);
        Assert.Equal(FakeOnlineVerdict.InsufficientEvidence, d.Record(false));
        Assert.Equal(FakeOnlineVerdict.FakeOnline, d.Record(false));
    }

    [Fact]
    public void 中途成功_计数清零()
    {
        var d = new FakeOnlineDetector(2, 2);
        d.Record(false);
        Assert.Equal(FakeOnlineVerdict.Healthy, d.Record(true));
        // 成功清零后需再连续失败两次才判定
        Assert.Equal(FakeOnlineVerdict.InsufficientEvidence, d.Record(false));
        Assert.Equal(FakeOnlineVerdict.FakeOnline, d.Record(false));
    }

    [Fact]
    public void 判定后计数自动清零_不会连坐()
    {
        var d = new FakeOnlineDetector(2, 2);
        d.Record(false);
        Assert.Equal(FakeOnlineVerdict.FakeOnline, d.Record(false));
        // 下一轮重新计数
        Assert.Equal(FakeOnlineVerdict.InsufficientEvidence, d.Record(false));
    }

    [Fact]
    public void 探测节奏_每N个tick一次()
    {
        var d = new FakeOnlineDetector(probeEveryNTicks: 3);
        Assert.False(d.ShouldProbeThisTick(0));
        Assert.False(d.ShouldProbeThisTick(1));
        Assert.False(d.ShouldProbeThisTick(2));
        Assert.True(d.ShouldProbeThisTick(3));
        Assert.False(d.ShouldProbeThisTick(4));
        Assert.True(d.ShouldProbeThisTick(6));
    }

    [Fact]
    public void Reset_清零计数()
    {
        var d = new FakeOnlineDetector(2, 2);
        d.Record(false);
        d.Reset();
        Assert.Equal(FakeOnlineVerdict.InsufficientEvidence, d.Record(false));
    }
}
