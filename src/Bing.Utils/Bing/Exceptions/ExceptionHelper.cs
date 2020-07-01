using System;
using System.Runtime.ExceptionServices;

namespace Bing.Exceptions
{
    /// <summary>
    /// 异常操作辅助类
    /// </summary>
    public static class ExceptionHelper
    {
        /// <summary>
        /// 捕抓异常并重新抛出
        /// </summary>
        /// <param name="exception">异常</param>
        public static Exception PrepareForRethrow(Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
            // The code cannot ever get here. We just return a value to work around a badly-designed API (ExceptionDispatchInfo.Throw):
            //  https://connect.microsoft.com/VisualStudio/feedback/details/689516/exceptiondispatchinfo-api-modifications (http://www.webcitation.org/6XQ7RoJmO)
            return exception;
        }
    }
}
