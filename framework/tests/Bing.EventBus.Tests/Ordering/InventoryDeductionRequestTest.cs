using System.Text;
using Bing.Data.Inventory;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证库存请求的精确身份及字符边界。
/// </summary>
public sealed class InventoryDeductionRequestTest
{
    /// <summary>
    /// 无效 UTF-16 代理字符不能成为持久化身份。
    /// </summary>
    /// <param name="position">需要替换的身份片段位置。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Constructor_WhenIdentityContainsUnpairedSurrogate_ShouldReject(int position)
    {
        var keys = new[] { "tenant", "warehouse", "sku", "request" };
        keys[position] = "invalid\ud800";
        Assert.Throws<EncoderFallbackException>(() =>
            new InventoryDeductionRequest(keys[0], keys[1], keys[2], 1, keys[3]));
    }

    /// <summary>
    /// 身份保持原值，不合并大小写、尾空格或 Unicode 形式。
    /// </summary>
    [Fact]
    public void Constructor_ShouldKeepExactIdentifiersAndAcceptCharacterLimits()
    {
        var tenant = new string('\u4e2d', 128);
        var warehouse = string.Concat(Enumerable.Repeat("\ud83e\udde9", 64));
        var sku = new string('\u6587', 256);
        var requestId = "Case-e\u0301 ";
        var request = new InventoryDeductionRequest(tenant, warehouse, sku, 1, requestId);

        Assert.Equal(tenant, request.TenantId);
        Assert.Equal(warehouse, request.WarehouseId);
        Assert.Equal(sku, request.Sku);
        Assert.Equal(requestId, request.RequestId);
        Assert.True(Encoding.UTF8.GetByteCount(tenant) <= 512);
        Assert.True(Encoding.UTF8.GetByteCount(warehouse) <= 512);
        Assert.True(Encoding.UTF8.GetByteCount(sku) <= 1024);
    }

    /// <summary>
    /// 超出身份字符上限的输入被拒绝。
    /// </summary>
    /// <param name="position">需要替换的身份片段位置。</param>
    /// <param name="limit">该片段允许的 UTF-16 长度。</param>
    [Theory]
    [InlineData(0, 128)]
    [InlineData(1, 128)]
    [InlineData(2, 256)]
    [InlineData(3, 128)]
    public void Constructor_WhenIdentityExceedsCharacterLimit_ShouldReject(int position, int limit)
    {
        var keys = new[] { "tenant", "warehouse", "sku", "request" };
        keys[position] = new string('a', limit + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InventoryDeductionRequest(keys[0], keys[1], keys[2], 1, keys[3]));
    }
}
