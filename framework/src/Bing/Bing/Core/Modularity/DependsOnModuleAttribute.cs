﻿namespace Bing.Core.Modularity;

/// <summary>
/// Bing 模块依赖
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnModuleAttribute : Attribute
{
    /// <summary>
    /// 当前模块的依赖模块类型集合
    /// </summary>
    public Type[] DependedModuleTypes { get; }

    /// <summary>
    /// 初始化一个<see cref="DependsOnModuleAttribute"/>类型的实例
    /// </summary>
    /// <param name="dependedModuleTypes">依赖模块类型集合</param>
    public DependsOnModuleAttribute(params Type[] dependedModuleTypes) => DependedModuleTypes = dependedModuleTypes ?? new Type[0];
}
