using System;
using Bing.Utils.Timing;
using Xunit;

namespace Bing.Utils.Tests.Timing
{
    public class DateTimeHelperTest
    {
        [Theory]
        [InlineData(DayOfWeek.Thursday, "2019/8/22 0:00:00")]
        [InlineData(DayOfWeek.Saturday, "2019/8/31 0:00:00")]
        [InlineData(DayOfWeek.Wednesday, "2020/1/22 0:00:00")]
        [InlineData(DayOfWeek.Friday, "2019/3/1 0:00:00")]
        [InlineData(DayOfWeek.Thursday, "2018/2/1 0:00:00")]
        public void Test_DateTimeHelper_GetWeekDay(DayOfWeek dw, string dateStr)
        {
            var res = DateTimeHelper.GetWeekDay(dateStr);
            Assert.Equal(dw, res);
        }
    }
}
