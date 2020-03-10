using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ingress.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Ingress
{
    public class ConfigEndpointDataSource : EndpointDataSource
    {
        private readonly object _lock;
        private IOptionsMonitor<IngressBindingOptions> _options;
        private readonly ILogger _logger;
        private List<Endpoint> _endpoints;
        private IChangeToken _changeToken;
        private CancellationTokenSource _cancellationTokenSource;

        public ConfigEndpointDataSource(IOptionsMonitor<IngressBindingOptions> options, ILogger logger)
        {
            _options = options;
            _logger = logger;
            _lock = new object();

            options.OnChange((s) =>
            {
                UpdateEndpoints();
            });
        }

        public override IChangeToken GetChangeToken()
        {
            Initialize();
            Debug.Assert(_changeToken != null);
            Debug.Assert(_endpoints != null);
            return _changeToken;
        }

        /// <summary>
        /// Returns a read-only collection of <see cref="Endpoint"/> instances.
        /// </summary>
        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                Initialize();
                return _endpoints;
            }
        }

        // Defer initialization to avoid doing lots of reflection on startup.
        // Note: we can't use DataSourceDependentCache here because we also need to handle a list of change
        // tokens, which is a complication most of our code doesn't have.
        private void Initialize()
        {
            lock (_lock)
            {
                if (_endpoints == null)
                {
                   UpdateEndpoints();
                }
            }
        }

        private void UpdateEndpoints()
        {
            lock (_lock)
            {
                _logger.LogInformation("Updating endpoints");
                var endpoints = CreateEndpoints();

                var oldCancellationTokenSource = _cancellationTokenSource;

                // Step 2 - update endpoints 
                _endpoints = endpoints;

                // Step 3 - create new change token
                _cancellationTokenSource = new CancellationTokenSource();
                _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);

                // Step 4 - trigger old token
                oldCancellationTokenSource?.Cancel();
            }
        }

        private List<Endpoint> CreateEndpoints()
        {
            var endpoints = new List<Endpoint>();
            foreach (var mapping in _options.CurrentValue.IpMappings)
            {
                // TODO IpAddresses needs to support dns names
                var ipEndpoints = new List<IPEndPoint>();
                _logger.LogInformation("Available IPs");

                foreach (var ip in mapping.IpAddresses)
                {
                    ipEndpoints.Add(new IPEndPoint(IPAddress.Parse(ip), mapping.Port));
                    _logger.LogInformation(ip.ToString());
                }

                var loadBalanceSelector = new LoadBalananceSelector(ipEndpoints);
                string pattern = $"{mapping.Path}{{**x}}";
                var routePattern = RoutePatternFactory.Parse(pattern);

                endpoints.Add(new RouteEndpoint(async c =>
                {
                    var ipEndpoint = await loadBalanceSelector.SelectAsync();

                    var uriBuilder = new UriBuilder();
                    uriBuilder.Host = ipEndpoint.Address.ToString();
                    uriBuilder.Scheme = mapping.Scheme;
                    uriBuilder.Path = c.Request.Path;
                    uriBuilder.Port = ipEndpoint.Port;

                    _logger.LogInformation(ipEndpoint.Address.ToString());
                    _logger.LogInformation(mapping.Scheme);
                    await c.ProxyRequest(uriBuilder.Uri);
                },
                routePattern,
                order: 0,
                EndpointMetadataCollection.Empty,
                mapping.Path));
            }
            return endpoints;
        }
    }
}