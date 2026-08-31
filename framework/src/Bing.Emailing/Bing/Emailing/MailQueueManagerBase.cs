using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bing.Emailing;

/// <summary>
/// 在后台线程中消费邮件队列的管理器基类。
/// </summary>
public abstract class MailQueueManagerBase : IMailQueueManager
{
    /// <summary>
    /// 提供待发送邮件出队操作的队列提供程序。
    /// </summary>
    private readonly IMailQueueProvider _mailQueueProvider;

    /// <summary>
    /// 提供队列空闲轮询间隔等邮件配置的提供器。
    /// </summary>
    private readonly IEmailConfigProvider _emailConfigProvider;

    /// <summary>
    /// 标记后台邮件发送循环应停止的标志。
    /// </summary>
    private bool _tryStop;

    /// <summary>
    /// 承载后台邮件发送循环的线程。
    /// </summary>
    private Thread _thread;

    /// <summary>
    /// 获取或设置后台邮件发送线程是否正在运行。
    /// </summary>
    public bool IsRunning { get; protected set; } = false;

    /// <summary>
    /// 获取当前待发送邮件数量。
    /// </summary>
    public int Count => _mailQueueProvider.Count;

    /// <summary>
    /// 初始化一个<see cref="MailQueueManagerBase"/>类型的实例
    /// </summary>
    /// <param name="emailConfigProvider">电子邮件配置提供器</param>
    /// <param name="mailQueueProvider">邮件队列提供程序</param>
    protected MailQueueManagerBase(IEmailConfigProvider emailConfigProvider, IMailQueueProvider mailQueueProvider)
    {
        _emailConfigProvider = emailConfigProvider;
        _mailQueueProvider = mailQueueProvider;
    }

    /// <summary>
    /// 运行
    /// </summary>
    public virtual void Run()
    {
        if (IsRunning || (_thread != null && _thread.IsAlive))
        {
            WriteLog("已经运行，又被启动了，新线程启动已经取消", LogLevel.Warning);
            return;
        }
        IsRunning = true;
        _thread = new Thread(StartSendMail)
        {
            Name = "EmailQueue",
            IsBackground = true
        };
        WriteLog("线程即将启动", LogLevel.Information);
        _thread.Start();
        WriteLog($"线程已经启动，线程ID是: {_thread.ManagedThreadId}", LogLevel.Information);
    }

    /// <summary>
    /// 停止
    /// </summary>
    public virtual void Stop()
    {
        if (_tryStop)
            return;
        _tryStop = true;
    }

    /// <summary>
    /// 在后台线程中循环消费并发送队列中的邮件。
    /// </summary>
    /// <remarks>停止标志设置后退出循环；队列为空时按 <see cref="EmailConfig.SleepInterval"/> 休眠。队列或发送过程发生异常时记录错误、终止当前工作线程，并在退出前复位运行状态。</remarks>
    protected void StartSendMail()
    {
        var sw = new Stopwatch();
        try
        {
            while (true)
            {
                if (_tryStop)
                    break;

                if (_mailQueueProvider.IsEmpty)
                {
                    WriteLog("队列是空，开始睡眠", LogLevel.Trace);
                    var config = _emailConfigProvider.GetConfig();
                    Thread.Sleep(config.SleepInterval);
                    continue;
                }

                if (_mailQueueProvider.TryDequeue(out var box))
                {
                    WriteLog($"开始发送邮件 标题：{box.Subject}，收件人：{box.To.First()}", LogLevel.Information);
                    sw.Restart();
                    SendMail(box);
                    sw.Stop();
                    WriteLog($"发送邮件结束 标题：{box.Subject}，收件人：{box.To.First()}，耗时：{sw.Elapsed.TotalSeconds}",
                        LogLevel.Information);
                }
            }
        }
        catch (Exception e)
        {
            WriteLog($"循环中出错，线程即将结束：{e.Message}", LogLevel.Error);
            IsRunning = false;
        }

        WriteLog("邮件发送线程即将停止，人为跳出循环，没有异常发生", LogLevel.Information);
        _tryStop = false;
        IsRunning = false;
    }

    /// <summary>
    /// 发送邮件
    /// </summary>
    /// <param name="box">电子邮件</param>
    protected abstract void SendMail(EmailBox box);

    /// <summary>
    /// 写入日志
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="level">日志等级</param>
    protected abstract void WriteLog(string log, LogLevel level);
}
