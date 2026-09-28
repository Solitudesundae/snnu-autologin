using System.Text.Json;
using System.Text.Json.Serialization;
using SnnuAutoLogin.Core;
using SnnuAutoLogin.Logging;

namespace SnnuAutoLogin.Security;

/// <summary>运营商服务类型（登录表单 yys 字段值）。</summary>
public static class ServiceTypes
{
    public sealed record Option(string Suffix, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    /// <summary>下拉框顺序即配置向导展示顺序；Suffix 为空串 = 校园网免费通道（不提交 yys 字段）。</summary>
    public static readonly Option[] All =
    {
        new("", "校园网（免费 2.5G/月）"),
        new("unicom", "中国联通"),
        new("mobile", "中国移动"),
        new("telecom", "中国电信"),
    };

    public static string DisplayNameOf(string suffix) =>
        All.FirstOrDefault(o => o.Suffix == suffix)?.DisplayName ?? suffix;
}

/// <summary>应用配置。密码仅以 DPAPI 密文形式存在于内存与磁盘。</summary>
public sealed class AppConfig
{
    public int Version { get; set; } = 1;

    /// <summary>学号（如 20231001）。</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>DPAPI(CurrentUser) 加密后的 Base64 密文。磁盘与日志中永无明文。</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>运营商后缀："" / unicom / mobile / telecom。</summary>
    public string ServiceSuffix { get; set; } = "unicom";

    /// <summary>是否已配置（含有效账号密码），决定启动时是否弹向导。</summary>
    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(StudentId) && !string.IsNullOrEmpty(EncryptedPassword);

    /// <summary>门户心跳间隔（秒）。</summary>
    public int HeartbeatSeconds { get; set; } = 60;

    /// <summary>开机自启动（HKCU Run）。</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>响应关键字词典覆盖（空 = 用内置默认）。</summary>
    public KeywordSet? Keywords { get; set; }

    /// <summary>门户端点覆盖（空 = 用内置默认）。</summary>
    public PortalEndpoints? Endpoints { get; set; }

    /// <summary>完整账号：学号 或 学号@运营商（用于流量查询与日志打码）。</summary>
    [JsonIgnore]
    public string FullAccount => string.IsNullOrEmpty(ServiceSuffix)
        ? StudentId
        : $"{StudentId}@{ServiceSuffix}";
}

/// <summary>
/// 配置存取：%APPDATA%\SnnuAutoLogin\config.json，原子写入（临时文件 + 替换），
/// 目录 ACL 天然继承用户配置隔离。
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _dir;
    private readonly string _file;
    private readonly object _lock = new();

    public ConfigStore(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnnuAutoLogin");
        _file = Path.Combine(_dir, "config.json");
    }

    public string ConfigDirectory => _dir;

    /// <summary>捕获目录：首次离线时保存门户页面原文，供接口文档核对（docs/01 §5）。</summary>
    public string CapturesDirectory => Path.Combine(_dir, "captures");

    public AppConfig Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_file))
                {
                    return new AppConfig();
                }
                var json = File.ReadAllText(_file);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                return cfg ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Log.Warn($"读取配置失败，使用默认配置: {ex.Message}");
                return new AppConfig();
            }
        }
    }

    public void Save(AppConfig config)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_dir);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
            File.Move(tmp, _file, overwrite: true);
        }
    }

    /// <summary>改密码专用：读配置 → 更新字段 → 回写，全程不落明文。</summary>
    public void UpdateCredentials(string studentId, string plainPassword, string serviceSuffix)
    {
        lock (_lock)
        {
            var cfg = Load();
            cfg.StudentId = studentId.Trim();
            cfg.EncryptedPassword = DpapiProtector.Protect(plainPassword);
            cfg.ServiceSuffix = serviceSuffix;
            Save(cfg);
        }
    }

    /// <summary>解密密码到内存（调用方负责不外泄；失败返回 null 视为需重新配置）。</summary>
    public static string? DecryptPassword(AppConfig cfg)
    {
        try
        {
            return string.IsNullOrEmpty(cfg.EncryptedPassword) ? null : DpapiProtector.Unprotect(cfg.EncryptedPassword);
        }
        catch (Exception ex)
        {
            Log.Warn($"密码解密失败（可能跨用户/跨机器复制了配置）: {ex.GetType().Name}");
            return null;
        }
    }
}
