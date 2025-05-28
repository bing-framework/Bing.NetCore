using Bing.Content;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Bing.AspNetCore.Mvc.ContentFormatters;

/// <summary>
/// 远程流内容 - 模型绑定器
/// </summary>
/// <remarks>
/// 用于处理 <see cref="RemoteStreamContent"/> 类型及其接口 <see cref="IRemoteStreamContent"/> 的绑定。
/// </remarks>
public class BingRemoteStreamContentModelBinder<TRemoteStreamContent> : IModelBinder
    where TRemoteStreamContent : class, IRemoteStreamContent
{
    /// <summary>
    /// 绑定模型数据，用于处理文件上传场景。
    /// </summary>
    /// <param name="bindingContext">模型绑定上下文</param>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
            throw new ArgumentNullException(nameof(bindingContext));
        var postedFiles = GetCompatibleCollection<TRemoteStreamContent>(bindingContext);
        // 确定要绑定的模型名称
        var modelName = bindingContext.IsTopLevelObject
            ? bindingContext.BinderModelName ?? bindingContext.FieldName
            : bindingContext.ModelName;

        // 尝试获取表单文件
        await GetFormFilesAsync(modelName, bindingContext, postedFiles);

        // 如果未找到文件，且模型名称与原始模型名称不同，尝试使用原始模型名称重新获取文件
        if (postedFiles.Count == 0 &&
            bindingContext.OriginalModelName != null &&
            !string.Equals(modelName, bindingContext.OriginalModelName, StringComparison.Ordinal) &&
            !modelName.StartsWith(bindingContext.OriginalModelName + "[", StringComparison.Ordinal) &&
            !modelName.StartsWith(bindingContext.OriginalModelName + ".", StringComparison.Ordinal))
        {
            modelName = ModelNames.CreatePropertyModelName(bindingContext.OriginalModelName, modelName);
            await GetFormFilesAsync(modelName, bindingContext, postedFiles);
        }

        object value;
        // 根据模型类型决定如何处理获取的文件集合
        if (bindingContext.ModelType == typeof(TRemoteStreamContent))
        {
            if (postedFiles.Count == 0)
                return;
            value = postedFiles.First();
        }
        else
        {
            if (postedFiles.Count == 0 && !bindingContext.IsTopLevelObject)
                return;
            var modelType = bindingContext.ModelType;
            value = modelType == typeof(TRemoteStreamContent[]) ? postedFiles.ToArray() : postedFiles;
        }

        // 添加到验证状态
        bindingContext.ValidationState.Add(value, new ValidationStateEntry { Key = modelName });

        // 设置模型状态
        bindingContext.ModelState.SetModelValue(modelName, rawValue: null, attemptedValue: null);
        // 设置绑定结果为成功
        bindingContext.Result = ModelBindingResult.Success(value);
    }

    /// <summary>
    /// 从请求中获取与特定模型名称匹配的表单文件，并将它们添加到提供的集合中。
    /// </summary>
    /// <param name="modelName">用于匹配表单文件名称的模型名称。</param>
    /// <param name="bindingContext">模型绑定上下文</param>
    /// <param name="postedFiles">收集从请求中提取的文件的集合。</param>
    private async Task GetFormFilesAsync(string modelName, ModelBindingContext bindingContext, ICollection<TRemoteStreamContent> postedFiles)
    {
        var request = bindingContext.HttpContext.Request;
        // 检查请求是否为表单内容类型
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            foreach (var file in form.Files)
            {
                // 跳过空文件
                if (file.Length == 0 && string.IsNullOrEmpty(file.FileName))
                    continue;
                // 如果文件名称与模型名称匹配，则添加到 postedFiles 集合
                if (file.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                    postedFiles.Add(new RemoteStreamContent(file.OpenReadStream(), file.FileName, file.ContentType, file.Length).As<TRemoteStreamContent>());
            }
        }
        else if (bindingContext.IsTopLevelObject)
        {
            // 非表单请求，如果为顶层对象，则尝试直接从请求体中读取内容
            postedFiles.Add(new RemoteStreamContent(request.Body, null, request.ContentType, request.ContentLength).As<TRemoteStreamContent>());
        }
    }

    /// <summary>
    /// 根据绑定上下文获取一个兼容集合实例用于模型绑定。
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="bindingContext">模型绑定上下文</param>
    /// <returns>
    /// 返回一个兼容的、类型为 <typeparamref name="T"/> 的集合实例。如果已存在一个兼容的集合且不是只读的，
    /// 该集合将被清空并返回。如果不存在兼容的集合或原始集合是只读的，将创建并返回一个新的集合实例。
    /// </returns>
    private static ICollection<T> GetCompatibleCollection<T>(ModelBindingContext bindingContext)
    {
        var model = bindingContext.Model;
        var modelType = bindingContext.ModelType;

        if (typeof(T).IsAssignableFrom(modelType))
            return new List<T>();

        if (modelType == typeof(T[]))
            return new List<T>();

        if (model is ICollection<T> collection && !collection.IsReadOnly)
        {
            collection.Clear();
            return collection;
        }

        if (modelType.IsAssignableFrom(typeof(List<T>)))
            return new List<T>();

        return (ICollection<T>)Activator.CreateInstance(modelType)!;
    }
}
