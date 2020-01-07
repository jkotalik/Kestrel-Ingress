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
        public List<string> IpAddresses { get; set; }
        public int Port {get; set; }
        public string Path { get; set; }
        public string Scheme { get; set; }
    }
}
