using System.Net.Http;

namespace SnnuAutoLogin.Core;

/// <summary>
/// 直连互联网探测（captive portal 检测第一通道）。
/// 双探测点并行、3 秒超时、任一命中即认为可直连：
///   - 小米 generate_204：返回 204 No Content 即直连；
///   - 微软 connecttest.txt：正文恰为 "Microsoft Connect Test" 即直连。
/// 未认证校园网内会被网关劫持（返回门户页/非 204），据此与门户状态页组成双通道判定。
/// </summary>
public sealed class DetectProber : IDisposable
{
    private const int ProbeTimeoutSeconds = 3;

    private static readonly (string Url, string? ExpectBody)[] Probes =
    {
        ("http://connect.rom.miui.com/generate_204", null),
        ("http://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test"),
    };

    private readonly HttpClient _http;

    public DetectProber(HttpMessageHandler? handler = null)
    {
        _http = handler == null
            ? new HttpClient(new HttpClientHandler { UseProxy = false }, disposeHandler: true)
            : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(ProbeTimeoutSeconds);
    }

    /// <summary>返回 true 表示可直连互联网（家庭 WiFi / 已认证校园网）。</summary>
    public async Task<bool> IsDirectInternetAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(ProbeTimeoutSeconds));
        try
        {
            var tasks = Probes.Select(p => ProbeOneAsync(p.Url, p.ExpectBody, cts.Token)).ToList();
            var all = Task.WhenAll(tasks);
            try
            {
                await all.WaitAsync(TimeSpan.FromSeconds(ProbeTimeoutSeconds + 1), ct);
            }
            catch (TimeoutException)
            {
                // 双探测点整体超时（网络栈异常），按不可直连处理
            }
            return tasks.Any(t => t.IsCompletedSuccessfully && t.Result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ProbeOneAsync(string url, string? expectBody, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            if (expectBody == null)
            {
                return (int)resp.StatusCode == 204;
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            return (int)resp.StatusCode == 200 &&
                   body.Trim().Equals(expectBody, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or TimeoutException)
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}
