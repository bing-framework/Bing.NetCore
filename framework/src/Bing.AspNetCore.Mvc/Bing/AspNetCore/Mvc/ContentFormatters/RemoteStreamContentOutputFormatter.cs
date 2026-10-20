using Bing.Content;
using Bing.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Bing.AspNetCore.Mvc.ContentFormatters;

/// <summary>
/// 远程流内容 - 自定义输出格式化器
/// </summary>
/// <remarks>
/// 用于处理实现了 <see cref="IRemoteStreamContent"/> 接口的对象的HTTP响应。
/// </remarks>
public class RemoteStreamContentOutputFormatter : OutputFormatter
{
    /// <summary>
    /// 初始化一个 <see cref="RemoteStreamContentOutputFormatter"/> 类型的实例。
    /// </summary>
    public RemoteStreamContentOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("*/*"));
    }

    /// <inheritdoc />
    protected override bool CanWriteType(Type type) => typeof(IRemoteStreamContent).IsAssignableFrom(type);

    /// <inheritdoc />
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        var remoteStream = (IRemoteStreamContent)context.Object;
        if (remoteStream != null)
        {
            context.HttpContext.Response.ContentType = remoteStream.ContentType;
            context.HttpContext.Response.ContentLength = remoteStream.ContentLength;
            if (!string.IsNullOrWhiteSpace(remoteStream.FileName) && !context.HttpContext.Response.Headers.ContainsKey(HeaderNames.ContentDisposition))
            {
                var contentDisposition = new ContentDispositionHeaderValue("attachment");
                contentDisposition.SetHttpFileName(remoteStream.FileName);
                context.HttpContext.Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
            }

            using (remoteStream)
                await remoteStream.GetStream().CopyToAsync(context.HttpContext.Response.Body);
        }
    }
}
