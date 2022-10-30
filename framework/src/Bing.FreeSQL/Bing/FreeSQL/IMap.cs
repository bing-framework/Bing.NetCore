using FreeSql;

namespace Bing.FreeSQL;

/// <summary>
/// 映射
/// </summary>
public interface IMap
{
    /// <summary>
    /// 映射配置
    /// </summary>
    /// <param name="modelBuilder">模型生成器</param>
    void Map(ICodeFirst modelBuilder);
}