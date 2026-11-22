using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>分页名称预览不消费游标，并只为缺失槽位生成名称。</summary>
public sealed class ParameterPageNamePreviewTest
{
    /// <summary>有无 offset 均避让不区分大小写的占用名称，重复预览保持一致。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PreviewPageNames_ShouldReuseOffsetWithoutConsumingCursor(bool existingOffset, bool collision)
    {
        var manager = new ParameterManager(TestDialect.Instance);
        if (existingOffset) manager.Add("@offset", 5);
        if (collision) manager.Add("@_P_0", 7);
        var expectedOffset = existingOffset ? "@offset" : collision ? "@_p_1" : "@_p_0";
        var expectedLimit = existingOffset ? collision ? "@_p_1" : "@_p_0" : collision ? "@_p_2" : "@_p_1";
        for (var index = 0; index < 2; index++)
        {
            manager.PreviewPageNames(out var offset, out var limit, existingOffset ? "@offset" : null);
            Assert.Equal(expectedOffset, offset);
            Assert.Equal(expectedLimit, limit);
        }
        Assert.Equal((existingOffset ? 1 : 0) + (collision ? 1 : 0), manager.Count);
        Assert.Equal(collision ? "@_p_1" : "@_p_0", manager.GenerateName());
    }
}
