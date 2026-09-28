namespace SnnuAutoLogin.Core;

/// <summary>假在线心跳判定的结论。</summary>
public enum FakeOnlineVerdict
{
    /// <summary>外网探测通过，一切正常。</summary>
    Healthy,

    /// <summary>外网探测失败但证据不足（未达连续阈值），继续观察。</summary>
    InsufficientEvidence,

    /// <summary>门户在线 + 外网连续 N 次不通 → 判定假在线，应触发自愈。</summary>
    FakeOnline,
}

/// <summary>
/// 假在线判定器：纯状态逻辑，便于单元测试。
/// 场景：门户状态页显示"已在线"，但联通代拨出口的转发已被网关/运营商老化，
/// Windows 显示地球（NCSI 探测失败）——门户在"说谎"。
/// 策略：每 N 个心跳 tick 做一次 generate_204 探测；连续 miss 达阈值才判定，
/// 单次失败（探测点抖动/瞬时网络闪断）不触发，避免误杀正常会话。
/// </summary>
public sealed class FakeOnlineDetector
{
    private int _strikes;

    /// <summary>每几个心跳 tick 探测一次外网（tick≈60s，2 即约 2 分钟一次）。</summary>
    public int ProbeEveryNTicks { get; }

    /// <summary>连续探测失败达到该次数判定假在线。</summary>
    public int StrikeThreshold { get; }

    public FakeOnlineDetector(int probeEveryNTicks = 2, int strikeThreshold = 2)
    {
        ProbeEveryNTicks = Math.Max(1, probeEveryNTicks);
        StrikeThreshold = Math.Max(1, strikeThreshold);
    }

    /// <summary>本 tick 是否应做外网探测。</summary>
    public bool ShouldProbeThisTick(int tick) => tick > 0 && tick % ProbeEveryNTicks == 0;

    /// <summary>记录一次外网探测结果并给出判定。</summary>
    public FakeOnlineVerdict Record(bool directInternetOk)
    {
        if (directInternetOk)
        {
            _strikes = 0;
            return FakeOnlineVerdict.Healthy;
        }
        if (++_strikes >= StrikeThreshold)
        {
            _strikes = 0;
            return FakeOnlineVerdict.FakeOnline;
        }
        return FakeOnlineVerdict.InsufficientEvidence;
    }

    public void Reset() => _strikes = 0;
}
