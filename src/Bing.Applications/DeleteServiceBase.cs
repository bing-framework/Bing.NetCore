using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications.Dtos;
using Bing.Datas.Queries;
using Bing.Datas.Stores;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Applications
{
    /// <summary>
    /// 删除服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public abstract class DeleteServiceBase<TEntity, TDto, TQueryParameter> : DeleteServiceBase<TEntity, TDto, TQueryParameter, Guid>
        where TEntity : class, IKey<Guid>, IVersion, new()
        where TDto : IResponse, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="DeleteServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="store">存储器</param>
        protected DeleteServiceBase(IUnitOfWork unitOfWork, IStore<TEntity, Guid> store) : base(unitOfWork, store)
        {
        }
    }


    /// <summary>
    /// 删除服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class DeleteServiceBase<TEntity, TDto, TQueryParameter, TKey>:QueryServiceBase<TEntity, TDto, TQueryParameter, TKey>,IDeleteService<TDto, TQueryParameter> 
        where TEntity : class, IKey<TKey>, IVersion, new()
        where TDto : IResponse, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// 存储器
        /// </summary>
        private readonly IStore<TEntity, TKey> _store;

        /// <summary>
        /// 实体描述
        /// </summary>
        protected string EntityDescription { get; }

        /// <summary>
        /// 初始化一个<see cref="DeleteServiceBase{TEntity,TDto,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="store">存储器</param>
        protected DeleteServiceBase(IUnitOfWork unitOfWork,IStore<TEntity, TKey> store) : base(store)
        {
            _unitOfWork = unitOfWork;
            _store = store;
            EntityDescription = Reflection.GetDisplayNameOrDescription<TEntity>();
        }

        #region WriteLog(写日志)

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="caption">标题</param>
        protected void WriteLog(string caption)
        {
            Log.Class(typeof(TEntity).FullName)
                .Caption(caption)
                .Info();
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="caption">标题</param>
        /// <param name="entity">实体</param>
        protected void WriteLog(string caption, TEntity entity)
        {
            AddLog(entity);
            WriteLog(caption);
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="caption">标题</param>
        /// <param name="entities">实体集合</param>
        protected void WriteLog(string caption, IList<TEntity> entities)
        {
            AddLog(entities);
            WriteLog(caption);
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="entity">实体</param>
        protected void AddLog(TEntity entity)
        {
            Log.BussinessId(entity.Id.SafeString());
            Log.Content(entity.ToString());
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="entities">实体集合</param>
        protected void AddLog(IList<TEntity> entities)
        {
            Log.BussinessId(entities.Select(t => t.Id).Join());
            foreach (var entity in entities)
            {
                Log.Content(entity.ToString());
            }
        }

        #endregion

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        public void Delete(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return;
            }

            var entities = _store.FindByIds(ids);
            if (entities?.Count == 0)
            {
                return;
            }

            DeleteBeofre(entities);
            _store.Remove(entities);
            _unitOfWork.Commit();
            DeleteAfter(entities);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        public async Task DeleteAsync(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return;
            }

            var entities = await _store.FindByIdsAsync(ids);
            if (entities?.Count == 0)
            {
                return;
            }

            DeleteBeofre(entities);
            await _store.RemoveAsync(entities);
            await _unitOfWork.CommitAsync();
            DeleteAfter(entities);
        }

        /// <summary>
        /// 删除前操作
        /// </summary>
        /// <param name="entities">实体集合</param>
        protected virtual void DeleteBeofre(List<TEntity> entities)
        {
        }

        /// <summary>
        /// 删除后操作
        /// </summary>
        /// <param name="entities">实体集合</param>
        protected virtual void DeleteAfter(List<TEntity> entities)
        {
            AddLog(entities);
            WriteLog($"删除{EntityDescription}成功");
        }

    }
}
