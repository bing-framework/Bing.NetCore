using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Bing.Extensions;

namespace Bing.Tests.Samples
{
    [Serializable]
    public class ParentSample
    {
        public ChildSample Child { get; set; }

        public string Name { get; set; }

        private string _name;

        public ParentSample()
        {
        }

        public ParentSample(ParentSample sample)
        {
            this.Child = sample.Child;
            this.Name = sample.Name;
            this._name = sample._name;
        }

        public ParentSample Clone()
        {
            return this.DeepClone();
        }

        public void SetName(string name)
        {
            _name = name;
        }

        public string GetName()
        {
            return _name;
        }
    }
}
