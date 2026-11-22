namespace Bing.Data.Sql.Configs;

/// <summary>
/// SQL 模型访问、参数绑定及结果读取的执行策略。
/// </summary>
public enum SqlExecutionMode
{
    /// <summary>保留 Dapper 兼容路径，实体写入优先使用已生成的访问器。</summary>
    Compatible,

    /// <summary>已注册类型使用生成式执行；未注册类型允许在发送命令前选择兼容路径。</summary>
    GeneratedPreferred,

    /// <summary>只接受明确注册的生成模型及静态访问器，不允许反射回退。</summary>
    GeneratedOnly
}
