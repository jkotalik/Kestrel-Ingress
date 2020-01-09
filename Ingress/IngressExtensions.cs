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
                    var ingressService = context.RequestServices.GetRequiredService<IngressService>();
                    var host = $"{requestMessage.RequestUri.Host}:{requestMessage.RequestUri.Port}";

                    // TODO if there is already an IP endpoint, reuse rather than reconstuction from a URI
                    // TODO customize this.
                    var endpoint = new HttpEndPoint(HttpConnectionKind.Http, requestMessage.RequestUri.Host, requestMessage.RequestUri.Port, "", destinationUri, maxConnections: 1);
                    await using var connection = await ingressService.Client.ConnectAsync(endpoint);
                    // TODO newing up the HttpClientProtocol news up a reader, which has state outside of the pipe
                    // This causes a protocol reusing a pipe to throw as the reader is in an invalid state
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