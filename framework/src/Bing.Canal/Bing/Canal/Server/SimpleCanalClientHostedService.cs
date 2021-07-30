using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Canal.Model;
using Bing.Canal.Server.Models;
using CanalSharp.Connections;
using CanalSharp.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Exception = System.Exception;
using Type = System.Type;

namespace Bing.Canal.Server
{
    /// <summary>
    /// Canal客户端 后台服务
    /// </summary>
    internal class SimpleCanalClientHostedService : IHostedService, IDisposable
    {
        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILogger<SimpleCanalClientHostedService> _logger;

        /// <summary>
        /// 日志工厂
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Canal选项配置
        /// </summary>
        private readonly CanalOptions _options;

        /// <summary>
        /// Canal 连接
        /// </summary>
        //private SimpleCanalConnection _canalConnection;
        private ClusterCanalConnection _canalConnection;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _isDispose = false;

        /// <summary>
        /// 作用域
        /// </summary>
        private readonly IServiceScope _scope;

        /// <summary>
        /// 配置
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 注册类型列表
        /// </summary>
        private readonly List<Type> _registerTypeList;

        /// <summary>
        /// 队列
        /// </summary>
        private readonly ConcurrentQueue<long> _queue = new ConcurrentQueue<long>();

        /// <summary>
        /// Canal队列
        /// </summary>
        private readonly ConcurrentQueue<CanalQueueData> _canalQueue = new ConcurrentQueue<CanalQueueData>();

        /// <summary>
        /// 重置标识
        /// </summary>
        private volatile bool _resetFlag = false;

        /// <summary>
        /// 自动重置事件
        /// </summary>
        private readonly AutoResetEvent _condition = new AutoResetEvent(false);

        /// <summary>
        /// 取消令牌源
        /// </summary>
        private readonly CancellationTokenSource _cts;

        public SimpleCanalClientHostedService(ILogger<SimpleCanalClientHostedService> logger
            , ILoggerFactory loggerFactory
            , IOptions<CanalOptions> options
            , IServiceScopeFactory serviceScopeFactory
            , IConfiguration configuration
            , CanalConsumeRegister register)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _registerTypeList = new List<Type>();
            if (register.SingletonConsumeList != null && register.SingletonConsumeList.Any())
                _registerTypeList.AddRange(register.SingletonConsumeList);
            if (register.ConsumeList != null && register.ConsumeList.Any())
                _registerTypeList.AddRange(register.ConsumeList);
            if (!_registerTypeList.Any())
                throw new ArgumentNullException(nameof(_registerTypeList));
            _configuration = configuration;
            _options = options?.Value;
            if (_options == null)
                _options = new CanalOptions();
            _scope = serviceScopeFactory.CreateScope();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                //_canalConnection = new SimpleCanalConnection(_options.Standalone, _loggerFactory.CreateLogger<SimpleCanalConnection>());
                _canalConnection = new ClusterCanalConnection(
                    new ClusterCanalOptions("10.186.135.38:2181,10.186.135.38:2182,10.186.135.38:2183", "12350")
                    {
                        Destination = "erp_test"
                    }, _loggerFactory);
                await _canalConnection.ConnectAsync();
                await _canalConnection.SubscribeAsync(_options.Filter);
                await _canalConnection.RollbackAsync(0);
                _logger.LogInformation("canal client start...");
                LazyCanalGetEntities();
                LazyCanalDoWork();
                CanalServerAckStart();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "canal client start error...");
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if(_isDispose)
                return;
            _isDispose = true;
            _cts.Cancel();
            try
            {
                _canalConnection.UnSubscribeAsync(_options.Filter).GetAwaiter().GetResult();
                _canalConnection.DisConnectAsync().GetAwaiter().GetResult();
                _logger.LogInformation("canal client stop success...");
            }
            catch (Exception e)
            {
                _logger.LogError(e,"canal client stop error...");
            }

            _canalConnection = null;
            _scope.Dispose();
        }

        private void CanalServerAckStart()
        {
            Task.Factory.StartNew(async () =>
            {
                _logger.LogInformation("canal-server ack worker thread start...");
                while (!_isDispose)
                {
                    try
                    {
                        if (_canalConnection == null)
                            continue;
                        if (!_queue.TryDequeue(out var batchId))
                            continue;
                        if (batchId > 0)
                            await _canalConnection
                                .AckAsync(batchId); // 如果程序突然关闭 canal service会关闭。这里就不会提交，下次重启应用消息会重复推送!
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// 获取实体列表
        /// </summary>
        private void LazyCanalGetEntities()
        {
            Task.Factory.StartNew(async () =>
            {
                _logger.LogInformation("canal receive worker thread start...");
                while (!_isDispose)
                {
                    try
                    {
                        if (!await PreparedAndEnqueueAsync())
                            continue;
                        // 队列里面先储备5批次，超过5批次的话，就开始消费一个批次再储备
                        if (_canalQueue.Count >= 5)
                        {
                            _logger.LogInformation("canal receive worker waitOne...");
                            _resetFlag = true;
                            _condition.WaitOne();
                            _resetFlag = false;
                            _logger.LogInformation("canal receive worker continue...");
                        }
                    }
                    catch (IOException io)
                    {
                        _logger.LogError(io, "canal receive data error...");
                        try
                        {
                            await _canalConnection.ReConnectAsync();
                        }
                        catch (Exception e)
                        {
                            //ignore
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "canal receive data error...");
                    }
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// 准备数据并入队
        /// </summary>
        private async Task<bool> PreparedAndEnqueueAsync()
        {
            if (_canalConnection == null)
                return false;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var messageList = await _canalConnection.GetWithoutAckAsync(_options.BatchSize);
            var batchId = messageList.Id;
            if (batchId < 1)
                return false;

            var body = new CanalBody(null, batchId);
            if (messageList.Entries.Count <= 0)
            {
                _canalQueue.Enqueue(new CanalQueueData { CanalBody = body });
                return false;
            }

            var canalBody = GetCanalBodyList(messageList.Entries, batchId);
            if (canalBody.Message == null || canalBody.Message.Count < 1)
            {
                _canalQueue.Enqueue(new CanalQueueData { CanalBody = body });
                return false;
            }
            stopwatch.Stop();

            var dotime = (int)stopwatch.Elapsed.TotalSeconds;
            var time = dotime > 1 ? ParseTimeSeconds(dotime) : $"{stopwatch.Elapsed.TotalMilliseconds}ms";
            var canalQueueData = new CanalQueueData()
            {
                Time = time,
                CanalBody = canalBody
            };
            _canalQueue.Enqueue(canalQueueData);
            return true;
        }

        private void LazyCanalDoWork()
        {
            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation("handler worker thread start...");
                while (!_isDispose)
                {
                    try
                    {
                        if (_canalConnection == null)
                            continue;
                        if (!_canalQueue.TryDequeue(out var canalData))
                            continue;
                        if (canalData == null)
                            continue;
                        if (_resetFlag && _canalQueue.Count <= 1)
                        {
                            _condition.Set();
                            _resetFlag = false;
                        }

                        if (canalData.CanalBody.Message == null)
                        {
                            if (canalData.CanalBody.BatchId > 0)
                                _queue.Enqueue(canalData.CanalBody.BatchId);
                            continue;
                        }

                        _logger.LogInformation(
                            $"【batchId:{canalData.CanalBody.BatchId}】batchCount:{canalData.CanalBody.Message.Count},batchGetTime:{canalData.Time}");

                        Send(canalData.CanalBody);
                        _queue.Enqueue(canalData.CanalBody.BatchId);
                    }
                    catch (Exception e)
                    {
                        return;
                    }
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void Send(CanalBody canalBody)
        {
            try
            {
                foreach (var type in _registerTypeList)
                {
                    var service = _scope.ServiceProvider.GetRequiredService(type) as INotificationHandler<CanalBody>;
                    service?.HandleAsync(canalBody).ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,"canal produce error,end process!");
                Dispose();
            }
        }

        /// <summary>
        /// 获取Canal内容
        /// </summary>
        /// <param name="entries">变更入口列表</param>
        /// <param name="batchId">批次标识</param>
        private CanalBody GetCanalBodyList(List<Entry> entries, long batchId)
        {
            var result = new List<DataChange>();
            foreach (var entry in entries)
            {
                // 忽略事务
                if (entry.EntryType == EntryType.Transactionbegin || entry.EntryType == EntryType.Transactionend)
                    continue;
                // 没有拿到数据库名称或数据表名称的直接排除
                if (string.IsNullOrEmpty(entry.Header.SchemaName) || string.IsNullOrEmpty(entry.Header.TableName))
                    continue;
                RowChange rowChange = null;
                try
                {
                    // 获取行变更
                    rowChange = RowChange.Parser.ParseFrom(entry.StoreValue);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"DbName:{entry.Header.SchemaName},TbName:{entry.Header.TableName} RowChange.Parser.ParseFrom error");
                    continue;
                }

                if (rowChange != null)
                {
                    // 变更类型 insert/update/delete 等等
                    var eventType = rowChange.EventType;
                    // 输出binlog信息 表名 数据库名 变更类型
                    _logger.LogInformation($"================> binlog[{entry.Header.LogfileName}:{entry.Header.LogfileOffset}] , name[{entry.Header.SchemaName},{entry.Header.TableName}] , eventType :{eventType}");
                    // 输出 insert/update/delete 变更类型列数据
                    foreach (var rowData in rowChange.RowDatas)
                    {
                        var dataChange = new DataChange()
                        {
                            DbName = entry.Header.SchemaName,
                            TableName = entry.Header.TableName,
                            CanalDestination = _options.Cluster.Destination
                        };
                        if (eventType == EventType.Delete)
                        {
                            dataChange.EventType = "DELETE";
                            dataChange.BeforeColumnList = rowData.BeforeColumns.ToList();
                        }
                        else if (eventType == EventType.Insert)
                        {
                            dataChange.EventType = "INSERT";
                            dataChange.AfterColumnList = rowData.AfterColumns.ToList();
                        }
                        else if (eventType == EventType.Update)
                        {
                            dataChange.EventType = "UPDATE";
                            dataChange.BeforeColumnList = rowData.BeforeColumns.ToList();
                            dataChange.AfterColumnList = rowData.AfterColumns.ToList();
                        }
                        else
                        {
                            continue;
                        }

                        var columns = dataChange.AfterColumnList == null || !dataChange.AfterColumnList.Any()
                            ? dataChange.BeforeColumnList
                            : dataChange.AfterColumnList;
                        var primaryKey = columns.FirstOrDefault(x => x.IsKey);
                        if (primaryKey == null || string.IsNullOrEmpty(primaryKey.Value))
                        {
                            // 没有主键
                            _logger.LogError($"DbName:{dataChange.DbName},TbName:{dataChange.TableName} without primaryKey :{JsonConvert.SerializeObject(dataChange)}");
                            continue;
                        }

                        result.Add(dataChange);
                    }
                }
            }

            return new CanalBody(result, batchId);
        }

        /// <summary>
        /// 将秒数转换为几天几小时
        /// </summary>
        /// <param name="t">秒数</param>
        /// <param name="type">0:转换后带秒, 1:转换后不带秒</param>
        /// <returns>几天几小时几分几秒</returns>
        private string ParseTimeSeconds(int t, int type = 0)
        {
            string result;
            int day, hour, minute, second;
            if (t >= 86400)//天
            {
                day = Convert.ToInt16(t / 86400);
                hour = Convert.ToInt16((t % 86400) / 3600);
                minute = Convert.ToInt16((t % 86400 % 3600) / 60);
                second = Convert.ToInt16(t % 86400 % 3600 % 60);
                result = type == 0 ? $"{day}D{hour}H{minute}M{second}S" : $"{day}D{hour}H{minute}M";
            }
            else if (t >= 3600)//时
            {
                hour = Convert.ToInt16(t / 3600);
                minute = Convert.ToInt16((t % 3600) / 60);
                second = Convert.ToInt16(t % 3600 % 60);
                result = type == 0 ? $"{hour}H{minute}M{second}S" : $"{hour}H{minute}M";
            }
            else if (t >= 60)//分
            {
                minute = Convert.ToInt16(t / 60);
                second = Convert.ToInt16(t % 60);
                result = $"{minute}M{second}S";
            }
            else
            {
                second = Convert.ToInt16(t);
                result = $"{second}S";
            }
            return result;
        }
    }
}
