using System.Text.RegularExpressions;

namespace SnnuAutoLogin.Core;

/// <summary>
/// 门户 HTML/文本响应解析器。纯函数集合，便于用真实采样样本做单元测试。
/// 所有解析都基于 docs/01 接口逆向文档的实测样本结构。
/// </summary>
public static partial class PortalResponseParser
{
    /// <summary>状态页隐藏域：&lt;input type="hidden" id="account" value="学号@运营商"/&gt;</summary>
    [GeneratedRegex(@"<input[^>]*id=""account""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AccountInputRegex();

    [GeneratedRegex(@"<input[^>]*id=""ipaddr""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex IpInputRegex();

    [GeneratedRegex("""登录时间：\s*([0-9\-: ]+)""", RegexOptions.Compiled)]
    private static partial Regex LoginTimeRegex();

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>判断页面是否为"已认证在线"状态页。</summary>
    public static bool LooksOnline(string html, KeywordSet keywords)
    {
        if (string.IsNullOrEmpty(html))
        {
            return false;
        }
        // 必须命中在线标记词之一（"当前登录账号" 或隐藏域），仅出现 account 字样不算
        return keywords.OnlineMarkers.Any(k => html.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判定登录 POST 响应属于哪一类结果。
    /// 检查顺序即优先级：验证码 → 密码错误 → 已在线 → 成功关键字 → 在线状态页 → 未知。
    /// 密码错误放在成功之前，防止"登录失败：密码错误"这类页面被"登录"字样误判。
    /// 实测（2026-09-23 采样）：门户登录成功时直接返回在线状态页（含"当前登录账号"），
    /// 故命中在线标记同样判为成功。
    /// </summary>
    public static AuthOutcome ClassifyLoginResponse(string html, KeywordSet keywords)
    {
        if (string.IsNullOrEmpty(html))
        {
            return AuthOutcome.UnknownFailure;
        }
        if (ContainsAny(html, keywords.CaptchaNeeded))
        {
            return AuthOutcome.CaptchaNeeded;
        }
        if (ContainsAny(html, keywords.PasswordError))
        {
            return AuthOutcome.PasswordError;
        }
        if (ContainsAny(html, keywords.AlreadyOnline))
        {
            return AuthOutcome.AlreadyOnline;
        }
        if (ContainsAny(html, keywords.LoginSuccess))
        {
            return AuthOutcome.Success;
        }
        if (LooksOnline(html, keywords))
        {
            return AuthOutcome.Success;
        }
        return AuthOutcome.UnknownFailure;
    }

    /// <summary>从状态页提取会话信息（账号、IP、登录时间）。解析失败的字段为 null。</summary>
    public static PortalSessionInfo? ExtractSession(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }
        var account = AccountInputRegex().Match(html).Groups[1].Value;
        if (string.IsNullOrWhiteSpace(account))
        {
            return null;
        }
        var ip = IpInputRegex().Match(html).Groups[1].Value;
        if (string.IsNullOrWhiteSpace(ip))
        {
            ip = null;
        }

        DateTime? loginTime = null;
        var timeText = LoginTimeRegex().Match(html).Groups[1].Value.Trim();
        if (DateTime.TryParse(timeText, out var parsed))
        {
            loginTime = parsed;
        }
        return new PortalSessionInfo(account, ip, loginTime);
    }

    /// <summary>
    /// 解析流量报表响应，形如 "总流量:6874.340,2635.943"（已用 MB, 剩余 MB）。
    /// </summary>
    public static FluxInfo? ParseFlux(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }
        var idx = body.IndexOf(':');
        if (idx < 0 || idx + 1 >= body.Length)
        {
            return null;
        }
        var parts = body[(idx + 1)..].Split(',');
        if (parts.Length < 2)
        {
            return null;
        }
        if (!decimal.TryParse(parts[0].Trim(), out var used) || !decimal.TryParse(parts[1].Trim(), out var free))
        {
            return null;
        }
        return new FluxInfo(used, free);
    }

    /// <summary>去除 HTML 标签与多余空白，用于气泡提示等纯文本场景。</summary>
    public static string ToPlainText(string html, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }
        var text = Whitespace.Replace(html, " ");
        var start = text.IndexOf('<');
        while (start >= 0)
        {
            var end = text.IndexOf('>', start + 1);
            if (end < 0)
            {
                break;
            }
            text = text.Remove(start, end - start + 1);
            start = text.IndexOf('<');
        }
        text = text.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static bool ContainsAny(string html, IEnumerable<string> keywords) =>
        keywords.Any(k => !string.IsNullOrWhiteSpace(k) && html.Contains(k, StringComparison.OrdinalIgnoreCase));
}
