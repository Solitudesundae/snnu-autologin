using System.Security.Cryptography;
using System.Text;

namespace SnnuAutoLogin.Security;

/// <summary>
/// Windows DPAPI（数据保护 API）封装。
/// - 作用域 CurrentUser：仅当前 Windows 账户可解密，免管理员权限；
/// - 附加熵（entropy）为应用固定随机值：即便同一用户的其他程序调用 Unprotect，
///   不带相同熵也无法解出密码，降低同账户内恶意进程窃取的可能；
/// - DPAPI 底层由 Windows 以用户凭据派生密钥，等价于系统级的"凭据保护"。
/// </summary>
public static class DpapiProtector
{
    // 固定熵：Guid 字节。修改此值将导致既有配置无法解密，发布后不可变更。
    private static readonly byte[] Entropy = new Guid("7f3a2c51-9e64-4b8a-bd27-c05a41e8f912").ToByteArray();

    public static string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string Unprotect(string base64)
    {
        var encrypted = Convert.FromBase64String(base64);
        var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }
}
