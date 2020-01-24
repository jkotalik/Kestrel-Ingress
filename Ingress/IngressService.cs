// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Bedrock.Framework;
using Microsoft.Extensions.Options;

namespace Ingress
{
    public class IngressService
    {
        public IngressService(IServiceProvider serviceProvider, IOptions<IngressOptions> options)
        {
            Client = new ClientBuilder(serviceProvider).UseSockets().UseConnectionPooling().Build();
            Options = options.Value;
        }

        public IngressOptions Options { get; set; }
        internal Client Client { get; private set; }
    }
}