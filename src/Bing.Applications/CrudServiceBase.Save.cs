using System;
using System.Threading.Tasks;
using Bing.Domains.ChangeTracking;
using Bing.Extensions;
using Bing.Logs.Extensions;

namespace Bing.Applications
{
    /// <summary>
    /// 增删改查服务 - Save
    /// </summary>
    public abstract partial class CrudServiceBase<TEntity, TDto, TRequest, TCreateRequest, TUpdateRequest, TQueryParameter, TKey>
    {
        #region Create(创建)

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">创建参数</param>
        public virtual string Create(TCreateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var entity = ToEntityFromCreateRequest(request);
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            Create(entity);
            return entity.Id.ToString();
        }

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
        /// <returns></returns>
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
        /// 修改
        /// </summary>
        /// <param name="request">修改参数</param>
        public virtual void Update(TUpdateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var entity = ToEntityFromUpdateRequest(request);
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            Update(entity);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
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
            .BussinessId(entity.Id.SafeString())
            .Content(changeValues.SafeString());

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">修改参数</param>
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

        #region Save(保存)

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        public virtual void Save(TRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            SaveBefore(request);
            var entity = ToEntity(request);
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (IsNew(request, entity))
            {
                Create(entity);
                request.Id = entity.Id.ToString();
            }
            else
            {
                Update(entity);
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
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
        protected virtual bool IsNew(TRequest request, TEntity entity)
        {
            return string.IsNullOrWhiteSpace(request.Id) || entity.Id.Equals(default(TKey));
        }

        /// <summary>
        /// 提交后操作 - 该方法由工作单元拦截器调用
        /// </summary>
        public void CommitAfter() => SaveAfter();

        /// <summary>
        /// 保存后操作
        /// </summary>
        protected virtual void SaveAfter() => WriteLog($"保存{EntityDescription}成功");

        #endregion
    }
}
