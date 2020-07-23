using System;
using System.Collections.Generic;

namespace Bing.Auditing
{
    /// <summary>
    /// 审计操作信息
    /// </summary>
    public class AuditOperationEntry
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// 执行功能路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 执行的功能名
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 当前用户标识
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 当前用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 当前用户昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 当前访问IP
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 当前访问UserAgent
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// 审计数据信息集合
        /// </summary>
        public ICollection<AuditEntityEntry> Entities { get; set; }
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 耗时。单位：毫秒
        /// </summary>
        public int Elapsed { get; set; }

        /// <summary>
        /// 添加审计数据信息集合
        /// </summary>
        /// <param name="entities">审计数据信息集合</param>
        public void AddEntities(IEnumerable<AuditEntityEntry> entities)
        {
            foreach (var entity in entities) 
                Entities.Add(entity);
        }

        /// <summary>
        /// 结束请求
        /// </summary>
        public void End()
        {
            EndTime = DateTime.Now;
            if (CreationTime == default)
                Elapsed = 0;
            else
                Elapsed = (int)(EndTime - CreationTime).TotalMilliseconds;
        }

        /// <summary>
        /// 输出变更信息
        /// </summary>
        public override string ToString() => $"[ENTITY: {GetType().Name}]; {{ 'ApplicationName': {ApplicationName}, 'Path': {Path}, 'Ip': {Ip}, 'UserAgent': {UserAgent}, 'EndTime': {EndTime:yyyy-MM-dd HH:mm:ss}, 'Elapsed': {Elapsed}";
    }
}
