using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Ingress.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Ingress
{
    public class ConfigEndpointDataSource : EndpointDataSource
    {
        private readonly object _lock;
        private IOptionsMonitor<IngressBindingOptions> _options;
        private List<Endpoint> _endpoints;
        private IChangeToken _changeToken;
        private CancellationTokenSource _cancellationTokenSource;

        public ConfigEndpointDataSource(IOptionsMonitor<IngressBindingOptions> options)
        {
            _options = options;
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
                var ipEndpoints = new List<IPEndPoint>();

                foreach (var ip in mapping.IpAddresses)
                {
                    ipEndpoints.Add(new IPEndPoint(IPAddress.Parse(ip), mapping.Port));
                }

                var loadBalanceSelector = new LoadBalananceSelector(ipEndpoints);
                var routePattern = RoutePatternFactory.Parse(mapping.Path);

                endpoints.Add(new RouteEndpoint(async c =>
                {
                    var client = new ClientBuilder(c.RequestServices).UseSockets().UseConnectionLogging().Build();
                    var ipEndpoint = await loadBalanceSelector.SelectAsync();
                    await using var connection = await client.ConnectAsync(ipEndpoint);
                    var httpProtocol = new HttpClientProtocol(connection);
                    // bug: bedrock doesn't set the host header.
                    var request = new HttpRequestMessage(HttpMethod.Get, c.Request.Path);
                    request.Headers.Host = ipEndpoint.ToString();
                    var response = await httpProtocol.SendAsync(request);
                    await response.Content.CopyToAsync(c.Response.Body);
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