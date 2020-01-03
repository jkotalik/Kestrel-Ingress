using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Ingress.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Ingress
{
    public class ConfigEndpointDataSource : EndpointDataSource
    {
        private IngressBindingOptions _options;
        private List<Endpoint> _endpoints = new List<Endpoint>();
        public ConfigEndpointDataSource(IngressBindingOptions options)
        {
            System.Console.WriteLine("In Constructor");
            _options = options;
            Temp();
        }

        private void Temp()
        {
            foreach (var mapping in _options.IpMappings)
            {
                var ipEndpoints = new List<IPEndPoint>();

                foreach (var ip in mapping.IpAddresses)
                {
                    ipEndpoints.Add(new IPEndPoint(IPAddress.Parse(ip), mapping.Port));
                }

                var loadBalanceSelector = new LoadBalananceSelector(ipEndpoints);
                var routePattern = RoutePatternFactory.Parse(mapping.Path);

                _endpoints.Add(new RouteEndpoint(async c =>
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
        }

        public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

        public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }

}