namespace System;

/// <summary>
/// 释放基类
/// </summary>
public abstract class Disposable : IDisposable
{
    /// <summary>
    /// 是否已释放
    /// </summary>
    protected bool Disposed { get; private set; }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放中</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            Disposed = true;
    }

    /// <summary>
    /// 释放资源。执行与释放或重置非托管资源关联的应用程序定义的任务
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~Disposable() => Dispose(false);
}
