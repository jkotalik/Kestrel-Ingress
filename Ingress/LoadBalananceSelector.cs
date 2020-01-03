using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Ingress
{
    internal class LoadBalananceSelector
    {
        private List<IPEndPoint> _endpoints;
        private int _roundRobin = 0;

        public LoadBalananceSelector(List<IPEndPoint> endpoints)
        {
            _endpoints = endpoints;
        }

        public ValueTask<IPEndPoint> SelectAsync()
        {
            return new ValueTask<IPEndPoint>(_endpoints[(_roundRobin++) % _endpoints.Count]);
        }
    }
}