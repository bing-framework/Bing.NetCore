using System;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MailKit.Tests;

public class TestBase
{
    protected ITestOutputHelper Output;

    public TestBase(ITestOutputHelper output)
    {
        Output = output;
    }
}