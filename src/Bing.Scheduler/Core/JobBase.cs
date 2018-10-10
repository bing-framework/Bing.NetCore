using System.ComponentModel.DataAnnotations;
using Bing.Scheduler.Abstractions;
using Newtonsoft.Json;

namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 任务基类
    /// </summary>
    public abstract class JobBase : IJob
    {
        /// <summary>
        /// 任务标识
        /// </summary>
        [StringLength(32)]
        public string Id { get; set; }

        /// <summary>
        /// 任务分组
        /// </summary>
        [StringLength(100)]
        public string Group { get; set; }

        /// <summary>
        /// 任务名称：完整的类名
        /// </summary>
        [Required]
        [StringLength(100)] public string Name { get; set; }

        /// <summary>
        /// 定时表达式
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Cron { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        [StringLength(500)]
        public string Content { get; set; }

        /// <summary>
        /// 重写转换成字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
