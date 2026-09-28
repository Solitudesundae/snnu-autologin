namespace SnnuAutoLogin.Core;

/// <summary>应用对外呈现的四个状态（对应用户可见的托盘图标颜色）。</summary>
public enum AppState
{
    /// <summary>离线：无网络、非校园网休眠、或因密码错误熔断。</summary>
    Offline,

    /// <summary>检测中：正在探测网络环境/门户状态。</summary>
    Detecting,

    /// <summary>认证中：正在提交登录请求。</summary>
    Authenticating,

    /// <summary>在线：校园网认证完成（或已确认可直连互联网）。</summary>
    Online,
}

/// <summary>状态迁移事件参数。</summary>
public sealed record StateChangedEventArgs(AppState From, AppState To, string Reason, DateTime At);

/// <summary>
/// 四态状态机（离线/检测中/认证中/在线）。线程安全；
/// 仅负责状态与迁移事件的登记，迁移语义（何时迁移）由 <see cref="AppCoordinator"/> 决定。
/// 迁移规则：
///   Offline → Detecting / Online（网络事件后快速确认）
///   Detecting → Authenticating / Online / Offline
///   Authenticating → Online / Offline（失败回落）
///   Online → Offline（掉线）/ Detecting（心跳发现异常，重新检测）
/// </summary>
public sealed class LoginStateMachine
{
    private readonly object _lock = new();

    public AppState Current { get; private set; } = AppState.Offline;

    public string CurrentReason { get; private set; } = "初始状态";

    public DateTime ChangedAt { get; private set; } = DateTime.Now;

    /// <summary>状态变化时触发（仅在状态真正变化时；同态不同原因只更新原因并触发 ReasonsOnly 通知）。</summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>同态但原因更新（例如仍在 Offline，但从"无网络"变为"密码错误熔断"）。</summary>
    public event EventHandler<StateChangedEventArgs>? ReasonChanged;

    /// <summary>
    /// 尝试迁移。返回 true 表示状态发生变化。
    /// 非法迁移（如 Offline→Authenticating）会抛 <see cref="InvalidOperationException"/>，
    /// 这是编程错误（协调器漏掉检测步骤）应当尽早暴露。
    /// </summary>
    public bool TransitionTo(AppState to, string reason)
    {
        lock (_lock)
        {
            var from = Current;
            if (from == to)
            {
                if (CurrentReason != reason)
                {
                    CurrentReason = reason;
                    ReasonChanged?.Invoke(this, new StateChangedEventArgs(from, to, reason, DateTime.Now));
                }
                return false;
            }
            EnsureAllowed(from, to);
            Current = to;
            CurrentReason = reason;
            ChangedAt = DateTime.Now;
            StateChanged?.Invoke(this, new StateChangedEventArgs(from, to, reason, DateTime.Now));
            return true;
        }
    }

    private static void EnsureAllowed(AppState from, AppState to)
    {
        bool allowed = (from, to) switch
        {
            (AppState.Offline, AppState.Detecting) => true,
            (AppState.Offline, AppState.Online) => true,
            (AppState.Detecting, AppState.Authenticating) => true,
            (AppState.Detecting, AppState.Online) => true,
            (AppState.Detecting, AppState.Offline) => true,
            (AppState.Authenticating, AppState.Online) => true,
            (AppState.Authenticating, AppState.Offline) => true,
            (AppState.Authenticating, AppState.Detecting) => true,
            (AppState.Online, AppState.Offline) => true,
            (AppState.Online, AppState.Detecting) => true,
            _ => false,
        };
        if (!allowed)
        {
            throw new InvalidOperationException($"非法状态迁移: {from} → {to}");
        }
    }
}
