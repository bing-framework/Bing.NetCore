using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications.Aspects;
using Bing.Applications.Dtos;
using Bing.Datas.Queries;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;
using Bing.Extensions.AutoMapper;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Applications
{
    /// <summary>
    /// 增删改查服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public abstract class CrudServiceBase<TEntity, TDto, TQueryParameter> :
        CrudServiceBase<TEntity, TDto, TDto, TDto, TDto, TQueryParameter, Guid>,
        ICrudService<TDto, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, Guid>, new()
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, Guid> repository) : base(unitOfWork,
            repository)
        {
        }
    }

    /// <summary>
    /// 增删改查服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudServiceBase<TEntity, TDto, TQueryParameter, TKey> :
        CrudServiceBase<TEntity, TDto, TDto, TQueryParameter, TKey>,
        ICrudService<TDto, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudServiceBase{TEntity,TDto,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork,
            repository)
        {
        }
    }

    /// <summary>
    /// 增删改查服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudServiceBase<TEntity, TDto, TRequest, TQueryParameter, TKey> :
        CrudServiceBase<TEntity, TDto, TRequest, TRequest, TRequest, TQueryParameter, TKey>,
        ICrudService<TDto, TRequest, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IDto, new()
        where TRequest : IRequest, IKey, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudServiceBase{TEntity,TDto,TRequest,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork,
            repository)
        {
        }
    }

    /// <summary>
    /// 增删改查服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudServiceBase<TEntity, TDto, TCreateRequest, TUpdateRequest, TQueryParameter, TKey>
        : CrudServiceBase<TEntity, TDto, TDto, TCreateRequest, TUpdateRequest, TQueryParameter, TKey>,
            ICrudService<TDto, TCreateRequest, TUpdateRequest, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IDto, new()
        where TCreateRequest : IRequest, new()
        where TUpdateRequest : IRequest, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudServiceBase{TEntity,TDto,TCreateRequest,TUpdateRequest,TQueryParameter,TKey}"/>
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork,
            repository)
        {
        }
    }

    /// <summary>
    /// 增删改查服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract partial class CrudServiceBase<TEntity, TDto, TRequest, TCreateRequest, TUpdateRequest,
            TQueryParameter, TKey>
        : QueryServiceBase<TEntity, TDto, TQueryParameter, TKey>,
            ICrudService<TDto, TRequest, TCreateRequest, TUpdateRequest, TQueryParameter>, ICommitAfter
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IResponse, new()
        where TRequest : IRequest, IKey, new()
        where TCreateRequest : IRequest, new()
        where TUpdateRequest : IRequest, new()
        where TQueryParameter : IQueryParameter
    {

        /// <summary>
        /// 工作单元
        /// </summary>
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// 仓储
        /// </summary>
        private readonly IRepository<TEntity, TKey> _repository;

        /// <summary>
        /// 实体描述
        /// </summary>
        protected string EntityDescription { get; }

        /// <summary>
        /// 初始化一个<see cref="CrudServiceBase{TEntity,TDto,TRequest,TCreateRequest,TUpdateRequest,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(repository)
        {
            _unitOfWork = unitOfWork;
            _repository = repository;
            EntityDescription = Reflection.GetDisplayNameOrDescription<TEntity>();
        }

        /// <summary>
        /// 转换为实体
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        protected virtual TEntity ToEntity(TRequest request)
        {
            return request.MapTo<TEntity>();
        }

        /// <summary>
        /// 创建参数转换为实体
        /// </summary>
        /// <param name="request">创建参数</param>
        /// <returns></returns>
        protected virtual TEntity ToEntityFromCreateRequest(TCreateRequest request)
        {
            if (typeof(TCreateRequest) == typeof(TRequest))
            {
                return ToEntity(Bing.Utils.Helpers.Conv.To<TRequest>(request));
            }

            return request.MapTo<TEntity>();
        }

        /// <summary>
        /// 修改参数转换为实体
        /// </summary>
        /// <param name="request">修改参数</param>
        /// <returns></returns>
        protected virtual TEntity ToEntityFromUpdateRequest(TUpdateRequest request)
        {
            if (typeof(TCreateRequest) == typeof(TRequest))
            {
                return ToEntity(Bing.Utils.Helpers.Conv.To<TRequest>(request));
            }

            return request.MapTo<TEntity>();
        }

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

            var entities = _repository.FindByIds(ids);
            if (entities?.Count == 0)
            {
                return;
            }

            DeleteBeofre(entities);
            _repository.Remove(entities);
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

            var entities = await _repository.FindByIdsAsync(ids);
            if (entities?.Count == 0)
            {
                return;
            }

            DeleteBeofre(entities);
            await _repository.RemoveAsync(entities);
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
