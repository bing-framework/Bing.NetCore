using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Application.Dtos;
using Bing.Data.Queries;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Bing.Domain.Repositories;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logs;
using Bing.ObjectMapping;
using Bing.Uow;

namespace Bing.Application.Services
{
    /// <summary>
    /// 增删改查应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public abstract class CrudAppServiceBase<TEntity, TDto, TQueryParameter> :
        CrudAppServiceBase<TEntity, TDto, TDto, TDto, TDto, TQueryParameter, Guid>,
        ICrudAppService<TDto, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, Guid>, new()
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudAppServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudAppServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, Guid> repository) : base(unitOfWork, repository)
        {
        }
    }

    /// <summary>
    /// 增删改查应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudAppServiceBase<TEntity, TDto, TQueryParameter, TKey> :
        CrudAppServiceBase<TEntity, TDto, TDto, TQueryParameter, TKey>,
        ICrudAppService<TDto, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudAppServiceBase{TEntity,TDto,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudAppServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork, repository)
        {
        }
    }

    /// <summary>
    /// 增删改查应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudAppServiceBase<TEntity, TDto, TRequest, TQueryParameter, TKey> :
        CrudAppServiceBase<TEntity, TDto, TRequest, TRequest, TRequest, TQueryParameter, TKey>,
        ICrudAppService<TDto, TRequest, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IDto, new()
        where TRequest : IRequest, IKey, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="CrudAppServiceBase{TEntity,TDto,TRequest,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudAppServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork, repository)
        {
        }
    }

    /// <summary>
    /// 增删改查应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudAppServiceBase<TEntity, TDto, TRequest, TCreateRequest, TUpdateRequest, TQueryParameter, TKey> :
        CrudAppServiceBase<TEntity, TDto, TCreateRequest, TUpdateRequest, TQueryParameter, TKey>,
        ICrudAppService<TDto, TRequest, TCreateRequest, TUpdateRequest, TQueryParameter>
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
        /// 初始化一个<see cref="CrudAppServiceBase{TEntity,TDto,TRequest,TCreateRequest,TUpdateRequest,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudAppServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork, repository)
        {
            _unitOfWork = unitOfWork;
            _repository = repository;
        }

        #region To(转换)

        /// <summary>
        /// 转换为实体
        /// </summary>
        /// <param name="request">请求参数</param>
        protected virtual TEntity ToEntity(TRequest request) => request.MapTo<TEntity>();

        /// <summary>
        /// 创建参数转换为实体
        /// </summary>
        /// <param name="request">创建参数</param>
        protected override TEntity ToEntityFromCreateRequest(TCreateRequest request) => typeof(TCreateRequest) == typeof(TRequest) ? ToEntity(Conv.To<TRequest>(request)) : request.MapTo<TEntity>();

        /// <summary>
        /// 修改参数转换为实体
        /// </summary>
        /// <param name="request">修改参数</param>
        protected override TEntity ToEntityFromUpdateRequest(TUpdateRequest request) => typeof(TCreateRequest) == typeof(TRequest) ? ToEntity(Conv.To<TRequest>(request)) : request.MapTo<TEntity>();

        #endregion

        #region Save(保存)

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task SaveAsync(TRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            SaveBefore(request);
            var entity = ToEntity(request);
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (IsNew(request, entity))
            {
                await CreateAsync(entity);
                request.Id = entity.Id.SafeString();
            }
            else
            {
                await UpdateAsync(entity);
            }
        }

        /// <summary>
        /// 保存前操作
        /// </summary>
        /// <param name="request">请求参数</param>
        protected virtual void SaveBefore(TRequest request)
        {
        }

        /// <summary>
        /// 是否创建
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <param name="entity">实体</param>
        protected virtual bool IsNew(TRequest request, TEntity entity) => string.IsNullOrWhiteSpace(request.Id) || entity.Id.Equals(default(TKey));

        /// <summary>
        /// 提交后操作 - 该方法由工作单元拦截器调用
        /// </summary>
        public void CommitAfter() => SaveAfter();

        /// <summary>
        /// 保存后操作
        /// </summary>
        protected virtual void SaveAfter() => WriteLog($"保存{EntityDescription}成功");

        #endregion

        #region BatchSave(批量保存)

        /// <summary>
        /// 批量保存
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        /// <param name="deleteList">删除列表</param>
        public virtual async Task<List<TDto>> SaveAsync(List<TRequest> addList, List<TRequest> updateList, List<TRequest> deleteList)
        {
            if (addList == null && updateList == null && deleteList == null)
                return new List<TDto>();
            addList ??= new List<TRequest>();
            updateList ??= new List<TRequest>();
            deleteList ??= new List<TRequest>();
            FilterList(addList, updateList, deleteList);
            var addEntities = ToEntities(addList);
            var updateEntities = ToEntities(updateList);
            var deleteEntities = ToEntities(deleteList);
            SaveBefore(addEntities, updateEntities, deleteEntities);
            await AddListAsync(addEntities);
            await UpdateListAsync(updateEntities);
            await DeleteListAsync(deleteEntities);
            await CommitAsync();
            SaveAfter(addEntities, updateEntities, deleteEntities);
            return GetResult(addEntities, updateEntities);
        }

        /// <summary>
        /// 转换成实体集合
        /// </summary>
        /// <param name="dtos">请求参数集合</param>
        private List<TEntity> ToEntities(List<TRequest> dtos) => dtos.Select(ToEntity).Distinct().ToList();

        /// <summary>
        /// 过滤列表
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        /// <param name="deleteList">删除列表</param>
        private void FilterList(List<TRequest> addList, List<TRequest> updateList, List<TRequest> deleteList)
        {
            FilterByDeleteList(addList, deleteList);
            FilterByDeleteList(updateList, deleteList);
        }

        /// <summary>
        /// 过滤需要删除的项
        /// </summary>
        /// <param name="list">数据源</param>
        /// <param name="deleteList">需要删除的列表</param>
        private void FilterByDeleteList(List<TRequest> list, List<TRequest> deleteList)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (deleteList.Any(d => d.Id == item.Id))
                    list.Remove(item);
            }
        }

        /// <summary>
        /// 保存前操作
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        /// <param name="deleteList">删除列表</param>
        protected virtual void SaveBefore(List<TEntity> addList, List<TEntity> updateList, List<TEntity> deleteList)
        {
        }

        /// <summary>
        /// 添加列表
        /// </summary>
        /// <param name="list">新增列表</param>
        private async Task AddListAsync(List<TEntity> list)
        {
            if (list.Count == 0)
                return;
            Log.Content("创建实体：");
            foreach (var entity in list)
                await CreateAsync(entity);
        }

        /// <summary>
        /// 更新列表
        /// </summary>
        /// <param name="list">修改列表</param>
        private async Task UpdateListAsync(List<TEntity> list)
        {
            if (list.Count == 0)
                return;
            Log.Content("修改实体：");
            foreach (var entity in list)
                await UpdateAsync(entity);
        }

        /// <summary>
        /// 删除列表
        /// </summary>
        /// <param name="list">删除列表</param>
        private async Task DeleteListAsync(List<TEntity> list)
        {
            if (list.Count == 0)
                return;
            Log.Content("删除实体：");
            foreach (var entity in list)
                await DeleteChildsAsync(entity);
        }

        /// <summary>
        /// 删除子节点集合
        /// </summary>
        /// <param name="parent">父节点</param>
        protected virtual async Task DeleteChildsAsync(TEntity parent) => await DeleteEntityAsync(parent);

        /// <summary>
        /// 删除实体
        /// </summary>
        /// <param name="entity">实体</param>
        protected async Task DeleteEntityAsync(TEntity entity)
        {
            await _repository.RemoveAsync(entity.Id);
            AddLog(entity);
        }

        /// <summary>
        /// 提交
        /// </summary>
        private async Task CommitAsync() => await _unitOfWork.CommitAsync();

        /// <summary>
        /// 保存后操作
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        /// <param name="deleteList">删除列表</param>
        protected virtual void SaveAfter(List<TEntity> addList, List<TEntity> updateList, List<TEntity> deleteList) => WriteLog($"保存{EntityDescription}成功");

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="addList">新增列表</param>
        /// <param name="updateList">修改列表</param>
        protected virtual List<TDto> GetResult(List<TEntity> addList, List<TEntity> updateList) => addList.Concat(updateList).Select(ToDto).ToList();

        #endregion
    }

    /// <summary>
    /// 增删改查应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class CrudAppServiceBase<TEntity, TDto, TCreateRequest, TUpdateRequest, TQueryParameter, TKey>:
        DeleteAppServiceBase<TEntity, TDto, TQueryParameter, TKey>,
        ICrudAppService<TDto, TCreateRequest, TUpdateRequest, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>, new()
        where TDto : IResponse, new()
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
        /// 初始化一个<see cref="CrudAppServiceBase{TEntity,TDto,TCreateRequest,TUpdateRequest,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="repository">仓储</param>
        protected CrudAppServiceBase(IUnitOfWork unitOfWork, IRepository<TEntity, TKey> repository) : base(unitOfWork, repository)
        {
            _unitOfWork = unitOfWork;
            _repository = repository;
        }

        #region To(转换)

        /// <summary>
        /// 创建参数转换为实体
        /// </summary>
        /// <param name="request">创建参数</param>
        protected virtual TEntity ToEntityFromCreateRequest(TCreateRequest request) => request.MapTo<TEntity>();

        /// <summary>
        /// 修改参数转换为实体
        /// </summary>
        /// <param name="request">修改参数</param>
        protected virtual TEntity ToEntityFromUpdateRequest(TUpdateRequest request) => request.MapTo<TEntity>();

        #endregion

        #region Create(创建)

        /// <summary>
        /// 创建实体
        /// </summary>
        /// <param name="entity">实体</param>
        protected void Create(TEntity entity)
        {
            CreateBefore(entity);
            entity.Init();
            _repository.Add(entity);
            CreateAfter(entity);
        }

        /// <summary>
        /// 创建前操作
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void CreateBefore(TEntity entity)
        {
        }

        /// <summary>
        /// 创建后操作
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void CreateAfter(TEntity entity) => AddLog(entity);

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">创建参数</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task<string> CreateAsync(TCreateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var entity = ToEntityFromCreateRequest(request);
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            await CreateAsync(entity);
            return entity.Id.ToString();
        }

        /// <summary>
        /// 创建实体
        /// </summary>
        /// <param name="entity">实体</param>
        protected async Task CreateAsync(TEntity entity)
        {
            CreateBefore(entity);
            await CreateBeforeAsync(entity);
            entity.Init();
            await _repository.AddAsync(entity);
            CreateAfter(entity);
            await CreateAfterAsync(entity);
        }

        /// <summary>
        /// 创建前操作
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual Task CreateBeforeAsync(TEntity entity) => Task.CompletedTask;

        /// <summary>
        /// 创建后操作
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual Task CreateAfterAsync(TEntity entity) => Task.CompletedTask;

        #endregion

        #region Update(修改)

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <exception cref="ArgumentNullException"></exception>
        protected void Update(TEntity entity)
        {
            var oldEntity = FindOldEntity(entity.Id);
            if (oldEntity == null)
                throw new ArgumentNullException(nameof(oldEntity));
            var changes = oldEntity.GetChanges(entity);
            UpdateBefore(entity);
            _repository.Update(entity);
            UpdateAfter(entity, changes);
        }

        /// <summary>
        /// 查找旧实体
        /// </summary>
        /// <param name="id">标识</param>
        protected virtual TEntity FindOldEntity(TKey id) => _repository.Find(id);

        /// <summary>
        /// 查找旧实体
        /// </summary>
        /// <param name="id">标识</param>
        protected virtual async Task<TEntity> FindOldEntityAsync(TKey id) => await _repository.FindAsync(id);

        /// <summary>
        /// 修改前操作
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void UpdateBefore(TEntity entity)
        {
        }

        /// <summary>
        /// 修改后操作
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="changeValues">变更值集合</param>
        protected virtual void UpdateAfter(TEntity entity, ChangedValueDescriptorCollection changeValues) => Log
            .BusinessId(entity.Id.SafeString())
            .Content(changeValues.SafeString());

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">修改参数</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task UpdateAsync(TUpdateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var entity = ToEntityFromUpdateRequest(request);
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            await UpdateAsync(entity);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <exception cref="ArgumentNullException"></exception>
        protected async Task UpdateAsync(TEntity entity)
        {
            var oldEntity = await FindOldEntityAsync(entity.Id);
            if (oldEntity == null)
                throw new ArgumentNullException(nameof(oldEntity));
            var changes = oldEntity.GetChanges(entity);
            UpdateBefore(entity);
            await UpdateBeforeAsync(entity);
            await _repository.UpdateAsync(entity);
            UpdateAfter(entity, changes);
            await UpdateAfterAsync(entity, changes);
        }

        /// <summary>
        /// 修改前操作
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual Task UpdateBeforeAsync(TEntity entity) => Task.CompletedTask;

        /// <summary>
        /// 修改后操作
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="changeValues">变更值集合</param>
        protected virtual Task UpdateAfterAsync(TEntity entity, ChangedValueDescriptorCollection changeValues) => Task.CompletedTask;

        #endregion
    }
}
