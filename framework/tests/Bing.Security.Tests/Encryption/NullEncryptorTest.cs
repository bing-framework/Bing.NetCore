using Bing.Security.Encryption;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Encryption;

/// <summary>
/// <see cref="NullEncryptor"/> 单元测试
/// </summary>
public class NullEncryptorTest
{
    private readonly IEncryptor _encryptor = NullEncryptor.Instance;

    // ═══════════════════════════════════════════════════════════
    // Encrypt
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Encrypt 对任何输入都应返回空字符串（空加密器不做实际加密）。
    /// </summary>
    [Fact]
    public void Encrypt_WithAnyData_ShouldReturnEmpty()
    {
        _encryptor.Encrypt("secret-password").ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：Encrypt 对 null 输入应返回空字符串，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void Encrypt_WithNull_ShouldReturnEmpty()
    {
        _encryptor.Encrypt(null!).ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：Encrypt 对空字符串输入应返回空字符串。
    /// </summary>
    [Fact]
    public void Encrypt_WithEmptyString_ShouldReturnEmpty()
    {
        _encryptor.Encrypt(string.Empty).ShouldBe(string.Empty);
    }

    // ═══════════════════════════════════════════════════════════
    // Decrypt
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Decrypt 对任何输入都应返回空字符串（空加密器不做实际解密）。
    /// </summary>
    [Fact]
    public void Decrypt_WithAnyData_ShouldReturnEmpty()
    {
        _encryptor.Decrypt("cipher-text").ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：Decrypt 对 null 输入应返回空字符串，不抛异常。
    /// </summary>
    [Fact]
    public void Decrypt_WithNull_ShouldReturnEmpty()
    {
        _encryptor.Decrypt(null!).ShouldBe(string.Empty);
    }

    // ═══════════════════════════════════════════════════════════
    // Instance 单例
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Instance 字段应为 NullEncryptor 类型，多次访问返回同一引用。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSingleton()
    {
        ReferenceEquals(NullEncryptor.Instance, NullEncryptor.Instance).ShouldBeTrue();
        NullEncryptor.Instance.ShouldBeOfType<NullEncryptor>();
    }
}
