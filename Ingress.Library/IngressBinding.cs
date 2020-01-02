using System;
using System.Collections.Generic;

namespace Ingress.Library
{
    public class IngressBindingOptions
    {
        public IList<IpMapping> IpMappings { get; set; }
    }

    public class IpMapping
    {
        public string IpAddress { get; set; }
        public int Port {get; set; }
        public string Path { get; set; }
    }
}
