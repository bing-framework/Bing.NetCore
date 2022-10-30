using System;

namespace Bing.Tests.Samples;

[Serializable]
public class ChildSample
{
    public OneSample One { get; set; }

    public OneSample Two { get; set; }

    public string Name { get; set; }
}