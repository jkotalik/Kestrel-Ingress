// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ingress;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                    ingressService.Logger.LogInformation($"Routing request to {requestMessage.RequestUri.ToString()}");
                    var responseMessage = await ingressService.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                    ingressService.Logger.LogInformation($"Request finished");

                    await context.CopyProxyHttpResponse(responseMessage);
                }
            }
        }
    }
}