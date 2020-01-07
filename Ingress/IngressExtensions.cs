// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Ingress;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Http
{
    public static class IngressExtensions
    {
        /// <summary>
        /// Forwards current request to the specified destination uri.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="destinationUri">Destination Uri</param>
        public static async Task ProxyRequest(this HttpContext context, Uri destinationUri)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (destinationUri == null)
            {
                throw new ArgumentNullException(nameof(destinationUri));
            }

            if (context.WebSockets.IsWebSocketRequest)
            {
                await context.AcceptProxyWebSocketRequest(destinationUri.ToWebSocketScheme());
            }
            else
            {
                using (var requestMessage = context.CreateProxyHttpRequest(destinationUri))
                {
                    var client = new ClientBuilder(context.RequestServices).UseSockets().Build();
                    var host = $"{requestMessage.RequestUri.Host}:{requestMessage.RequestUri.Port}";
                    
                    // TODO if there is already an IP endpoint, reuse rather than reconstuction from a URI
                    await using var connection = await client.ConnectAsync(IPEndPoint.Parse(host));
                    var httpProtocol = new HttpClientProtocol(connection);

                    // bug: bedrock doesn't set the host header.
                    requestMessage.Headers.Host = host;
                    var responseMessage = await httpProtocol.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                    await context.CopyProxyHttpResponse(responseMessage);
                }
            }
        }
    }
}