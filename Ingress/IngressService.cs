// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.Options;

namespace Ingress
{
    public class IngressService
    {
        public IngressService(IServiceProvider serviceProvider, IOptions<IngressOptions> options)
        {
            Options = options.Value;
            Client = new HttpClient(Options.MessageHandler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        }

        public IngressOptions Options { get; set; }
        internal HttpClient Client { get; private set; }
    }
}