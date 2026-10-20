using System;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using Xunit;

namespace Bing.Tests.XUnitHelpers;

/// <summary>
/// 断言助手回归测试
/// </summary>
public class AssertHelperTest
{
    [Fact]
    public void Throws_DirectException_CapturesOnceAndChecksKeyword()
    {
        var calls = 0;
        var expected = new InvalidOperationException("expected keyword");

        var actual = AssertHelper.Throws<InvalidOperationException>(() =>
        {
            calls++;
            throw expected;
        }, "keyword");

        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_AspectInvocationException_CapturesInnerExceptionOnce()
    {
        var calls = 0;
        var expected = new InvalidOperationException("expected keyword");

        var actual = AssertHelper.Throws<InvalidOperationException>(() =>
        {
            calls++;
            throw new AspectInvocationException(null, expected);
        }, "keyword");

        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_WrongDirectException_FailsAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentException("wrong type");

        var failure = Record.Exception(() => AssertHelper.Throws<InvalidOperationException>(() =>
        {
            calls++;
            throw unexpected;
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_WrongWrappedException_FailsAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentException("wrong type");
        var wrapper = new AspectInvocationException(null, unexpected);

        var failure = Record.Exception(() => AssertHelper.Throws<InvalidOperationException>(() =>
        {
            calls++;
            throw wrapper;
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.NotSame(wrapper, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_DerivedDirectException_IsRejectedAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentNullException("parameter");

        var failure = Record.Exception(() => AssertHelper.Throws<ArgumentException>(() =>
        {
            calls++;
            throw unexpected;
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_DerivedWrappedException_IsRejectedAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentNullException("parameter");
        var wrapper = new AspectInvocationException(null, unexpected);

        var failure = Record.Exception(() => AssertHelper.Throws<ArgumentException>(() =>
        {
            calls++;
            throw wrapper;
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.NotSame(wrapper, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_NoException_FailsAfterSingleInvocation()
    {
        var calls = 0;

        var failure = Record.Exception(() => AssertHelper.Throws<InvalidOperationException>(() => calls++));

        Assert.NotNull(failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Throws_KeywordDoesNotMatch_FailsAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new InvalidOperationException("expected message");

        var failure = Record.Exception(() => AssertHelper.Throws<InvalidOperationException>(() =>
        {
            calls++;
            throw unexpected;
        }, "missing keyword"));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_DirectException_CapturesOnceAndChecksKeyword()
    {
        var calls = 0;
        var expected = new InvalidOperationException("expected keyword");

        var actual = await AssertHelper.ThrowsAsync<InvalidOperationException>(() =>
        {
            calls++;
            return Task.FromException(expected);
        }, "keyword");

        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_AspectInvocationException_CapturesInnerExceptionOnce()
    {
        var calls = 0;
        var expected = new InvalidOperationException("expected keyword");

        var actual = await AssertHelper.ThrowsAsync<InvalidOperationException>(() =>
        {
            calls++;
            return Task.FromException(new AspectInvocationException(null, expected));
        }, "keyword");

        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_WrongDirectException_FailsAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentException("wrong type");

        var failure = await Record.ExceptionAsync(() => AssertHelper.ThrowsAsync<InvalidOperationException>(() =>
        {
            calls++;
            return Task.FromException(unexpected);
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_WrongWrappedException_FailsAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentException("wrong type");
        var wrapper = new AspectInvocationException(null, unexpected);

        var failure = await Record.ExceptionAsync(() => AssertHelper.ThrowsAsync<InvalidOperationException>(() =>
        {
            calls++;
            return Task.FromException(wrapper);
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.NotSame(wrapper, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_DerivedDirectException_IsRejectedAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentNullException("parameter");

        var failure = await Record.ExceptionAsync(() => AssertHelper.ThrowsAsync<ArgumentException>(() =>
        {
            calls++;
            return Task.FromException(unexpected);
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_DerivedWrappedException_IsRejectedAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new ArgumentNullException("parameter");
        var wrapper = new AspectInvocationException(null, unexpected);

        var failure = await Record.ExceptionAsync(() => AssertHelper.ThrowsAsync<ArgumentException>(() =>
        {
            calls++;
            return Task.FromException(wrapper);
        }));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.NotSame(wrapper, failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_NoException_FailsAfterSingleInvocation()
    {
        var calls = 0;

        var failure = await Record.ExceptionAsync(() => AssertHelper.ThrowsAsync<InvalidOperationException>(() =>
        {
            calls++;
            return Task.CompletedTask;
        }));

        Assert.NotNull(failure);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ThrowsAsync_KeywordDoesNotMatch_FailsAfterSingleInvocation()
    {
        var calls = 0;
        var unexpected = new InvalidOperationException("expected message");

        var failure = await Record.ExceptionAsync(() => AssertHelper.ThrowsAsync<InvalidOperationException>(() =>
        {
            calls++;
            return Task.FromException(unexpected);
        }, "missing keyword"));

        Assert.NotNull(failure);
        Assert.NotSame(unexpected, failure);
        Assert.Equal(1, calls);
    }
}
