using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Utils.IO
{
    /// <summary>
    /// 文件监控器
    /// </summary>
    public class FileWatcher
    {
        /// <summary>
        /// 接受文件监控信息的事件委托
        /// </summary>
        /// <param name="sender">事件发送器</param>
        /// <param name="args">文件监控事件参数</param>
        public delegate void FileWatcherEventHandler(object sender, FileWatcherEventArgs args);

        /// <summary>
        /// 获取文件监控信息
        /// </summary>
        public FileWatcherEventHandler EventHandler;

        /// <summary>
        /// 执行间隔
        /// </summary>
        private int _interval = 10 * 1000;

        /// <summary>
        /// 文件更改监控已启动
        /// </summary>
        public bool IsWatching { get; private set; } = false;

        /// <summary>
        /// 监控器字典
        /// </summary>
        private ConcurrentDictionary<string, FileSystemWatcher> _watchers =
            new ConcurrentDictionary<string, FileSystemWatcher>();

        /// <summary>
        /// 初始化一个<see cref="FileWatcher"/>类型的实例
        /// </summary>
        /// <param name="paths">路径</param>
        public FileWatcher(string[] paths)
        {
            if (paths.IsEmpty())
            {
                return;
            }
            foreach (var path in paths)
            {
                AddPath(path);
            }
        }

        /// <summary>
        /// 添加路径
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public bool AddPath(string path)
        {
            if (Directory.Exists(path) && !_watchers.ContainsKey(path))
            {
                return _watchers.TryAdd(path, null);
            }

            return false;
        }

        /// <summary>
        /// 删除路径
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public bool DeletePath(string path)
        {
            if (_watchers.ContainsKey(path))
            {
                if (_watchers.TryRemove(path, out var temp) && temp != null)
                {
                    temp.EnableRaisingEvents = false;
                    temp.Dispose();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 启动文件监控
        /// </summary>
        public void Start()
        {
            if (IsWatching)
            {
                return;
            }

            IsWatching = true;
            Task.Factory.StartNew(() =>
            {
                while (IsWatching)
                {
                    foreach (var watcher in _watchers)
                    {
                        if (watcher.Value == null)
                        {
                            if (Directory.Exists(watcher.Key))
                            {
                                _watchers.TryUpdate(watcher.Key, CreateWatcher(watcher.Key), null);
                            }
                        }
                        else
                        {
                            if (!Directory.Exists(watcher.Key))
                            {
                                watcher.Value.EnableRaisingEvents = false;
                                watcher.Value.Dispose();
                                _watchers.TryUpdate(watcher.Key, null, watcher.Value);
                            }
                        }
                    }

                    Thread.Sleep(_interval);
                }

                IsWatching = false;
            });
        }

        /// <summary>
        /// 停止文件监控
        /// </summary>
        public void Stop()
        {
            IsWatching = false;
            foreach (var item in _watchers.Keys)
            {
                if (_watchers.ContainsKey(item))
                {
                    if (_watchers[item] != null)
                    {
                        _watchers[item].EnableRaisingEvents = false;
                    }

                    if (_watchers.TryRemove(item, out var temp) && temp != null)
                    {
                        temp.EnableRaisingEvents = false;
                        temp.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// 创建监控器
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        private FileSystemWatcher CreateWatcher(string path)
        {
            var fsw = new FileSystemWatcher(path);
            fsw.Created += (sender, e) =>
            {
                EventHandler?.Invoke(sender,
                    new FileWatcherEventArgs(e.ChangeType, e.FullPath, Path.GetFileName(e.FullPath), null, null));
            };
            fsw.Changed += (sender, e) =>
            {
                EventHandler?.Invoke(sender,
                    new FileWatcherEventArgs(e.ChangeType, e.FullPath, Path.GetFileName(e.FullPath), null, null));
            };
            fsw.Deleted += (sender, e) =>
            {
                EventHandler?.Invoke(sender,
                    new FileWatcherEventArgs(e.ChangeType, e.FullPath, Path.GetFileName(e.FullPath), null, null));
            };
            fsw.Renamed += (sender, e) =>
            {
                EventHandler?.Invoke(sender,
                    new FileWatcherEventArgs(e.ChangeType, e.FullPath, Path.GetFileName(e.FullPath), e.OldFullPath,
                        e.OldName));
            };
            fsw.Error += (sender, e) => { };
            fsw.IncludeSubdirectories = true;
            fsw.NotifyFilter = (NotifyFilters)383;
            fsw.EnableRaisingEvents = true;
            return fsw;
        }        
    }

    /// <summary>
    /// 文件监控事件参数
    /// </summary>
    public class FileWatcherEventArgs
    {
        /// <summary>
        /// 变更类型
        /// </summary>
        public WatcherChangeTypes ChangeTypes { get; }

        /// <summary>
        /// 文件全路径
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// 文件名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 旧的文件全路径
        /// </summary>
        public string OldFullPath { get; }

        /// <summary>
        /// 旧的文件名称
        /// </summary>
        public string OldName { get; }

        /// <summary>
        /// 初始化一个<see cref="FileWatcherEventArgs"/>类型的实例
        /// </summary>
        /// <param name="type">监控变更类型</param>
        /// <param name="fullPath">文件全路径</param>
        /// <param name="name">文件名称</param>
        /// <param name="oldFullPath">旧的文件全路径</param>
        /// <param name="oldName">旧的文件名称</param>
        public FileWatcherEventArgs(WatcherChangeTypes type, string fullPath, string name, string oldFullPath,
            string oldName)
        {
            ChangeTypes = type;
            FullPath = fullPath;
            Name = name;
            OldFullPath = oldFullPath;
            OldName = oldName;
        }
    }
}
