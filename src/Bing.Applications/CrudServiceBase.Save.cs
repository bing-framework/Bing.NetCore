using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Domains.Entities;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;

namespace Bing.Applications
{
    /// <summary>
    /// 增删改查服务 - Save
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TRequest">请求参数类型</typeparam>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>

    public abstract partial class CrudServiceBase<TEntity, TDto, TRequest, TCreateRequest, TUpdateRequest, TQueryParameter, TKey>
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">创建参数</param>
        public string Create(TCreateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var entity = ToEntityFromCreateRequest(request);
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
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
        protected virtual void CreateAfter(TEntity entity)
        {
            AddLog(entity);
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">创建参数</param>
        /// <returns></returns>
        public async Task<string> CreateAsync(TCreateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var entity = ToEntityFromCreateRequest(request);
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            await CreateAsync(entity);
            return entity.Id.ToString();
        }

        /// <summary>
        /// 创建实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        protected async Task CreateAsync(TEntity entity)
        {
            CreateBefore(entity);
            entity.Init();
            await _repository.AddAsync(entity);
            CreateAfter(entity);
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">修改参数</param>
        public void Update(TUpdateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var entity = ToEntityFromUpdateRequest(request);
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            Update(entity);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        protected void Update(TEntity entity)
        {
            var oldEntity = _repository.Find(entity.Id);
            if (oldEntity == null)
            {
                throw new ArgumentNullException(nameof(oldEntity));
            }
            var changes = oldEntity.GetChanges(entity);
            UpdateBefore(entity);
            _repository.Update(entity);
            UpdateAfter(entity, changes);
        }

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
        protected virtual void UpdateAfter(TEntity entity, ChangeValueCollection changeValues)
        {
            Log.BussinessId(entity.Id.SafeString()).Content(changeValues.SafeString());
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">修改参数</param>
        /// <returns></returns>
        public async Task UpdateAsync(TUpdateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var entity = ToEntityFromUpdateRequest(request);
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            await UpdateAsync(entity);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        protected async Task UpdateAsync(TEntity entity)
        {
            var oldEntity = await _repository.FindAsync(entity.Id);
            if (oldEntity == null)
            {
                throw new ArgumentNullException(nameof(oldEntity));
            }
            var changes = oldEntity.GetChanges(entity);
            UpdateBefore(entity);
            await _repository.UpdateAsync(entity);
            UpdateAfter(entity, changes);
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        public void Save(TRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            SaveBefore(request);
            var entity = ToEntity(request);
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
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
        /// <returns></returns>
        public async Task SaveAsync(TRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            SaveBefore(request);
            var entity = ToEntity(request);
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
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
        /// <returns></returns>
        protected virtual bool IsNew(TRequest request, TEntity entity)
        {
            return string.IsNullOrWhiteSpace(request.Id) || entity.Id.Equals(default(TKey));
        }

        /// <summary>
        /// 提交后操作 - 该方法由工作单元拦截器调用
        /// </summary>
        public void CommitAfter()
        {
            SaveAfter();
        }

        /// <summary>
        /// 保存后操作
        /// </summary>
        protected virtual void SaveAfter()
        {
            WriteLog($"保存{EntityDescription}成功");
        }
    }
}
