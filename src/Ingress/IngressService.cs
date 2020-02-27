// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Ingress
{
    public class IngressService
    {
        public IngressService(ILoggerFactory factory, IServiceProvider serviceProvider, IOptions<IngressOptions> options)
        {
            Logger = factory.CreateLogger("Ingress");
            Options = options.Value;
            Client = new HttpClient(Options.MessageHandler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        }

        public IngressOptions Options { get; set; }
        internal HttpClient Client { get; private set; }
        public ILogger Logger { get; }
    }
}