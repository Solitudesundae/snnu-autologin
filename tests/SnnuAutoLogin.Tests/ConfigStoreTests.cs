using System.Text.Json;
using SnnuAutoLogin.Security;
using Xunit;

namespace SnnuAutoLogin.Tests;

/// <summary>配置存储与 DPAPI 加密测试。运行在真实 Windows 用户上下文中（CI 需 Windows runner）。</summary>
public class ConfigStoreTests : IDisposable
{
    private readonly string _dir;

    public ConfigStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "snnu_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    [Fact]
    public void 默认配置_未配置状态()
    {
        var store = new ConfigStore(_dir);
        var cfg = store.Load();
        Assert.False(cfg.IsConfigured);
    }

    [Fact]
    public void 保存凭据_落盘为密文_可解密还原()
    {
        var store = new ConfigStore(_dir);
        store.UpdateCredentials("20231001", "P@ss w0rd测试", "unicom");

        var cfg = store.Load();
        Assert.True(cfg.IsConfigured);
        Assert.Equal("20231001", cfg.StudentId);
        Assert.Equal("unicom", cfg.ServiceSuffix);
        Assert.Equal("20231001@unicom", cfg.FullAccount);
        Assert.NotEqual("P@ss w0rd测试", cfg.EncryptedPassword);

        // DPAPI 往返
        Assert.Equal("P@ss w0rd测试", ConfigStore.DecryptPassword(cfg));
    }

    [Fact]
    public void 配置文件中不出现明文密码()
    {
        var store = new ConfigStore(_dir);
        store.UpdateCredentials("20231001", "PlainSecret99", "unicom");

        var raw = File.ReadAllText(Path.Combine(_dir, "config.json"));
        Assert.DoesNotContain("PlainSecret99", raw);
        // 且是合法 JSON
        using var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.TryGetProperty("EncryptedPassword", out _));
    }

    [Fact]
    public void 篡改密文_解密失败返回null()
    {
        var store = new ConfigStore(_dir);
        store.UpdateCredentials("20231001", "right-password", "unicom");
        File.WriteAllText(Path.Combine(_dir, "config.json"),
            JsonSerializer.Serialize(new AppConfig
            {
                StudentId = "20231001",
                EncryptedPassword = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
            }));
        Assert.Null(ConfigStore.DecryptPassword(store.Load()));
    }

    [Fact]
    public void 自定义配置目录_文件写入该目录()
    {
        var store = new ConfigStore(_dir);
        store.UpdateCredentials("12345678", "pw", "mobile");
        Assert.True(File.Exists(Path.Combine(_dir, "config.json")));
        Assert.Equal(Path.Combine(_dir, "captures"), store.CapturesDirectory);
    }
}
