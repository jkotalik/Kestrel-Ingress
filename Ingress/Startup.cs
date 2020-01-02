using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Ingress.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ingress
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<KestrelServerOptions>(
                Configuration.GetSection("Kestrel"));
            services.Configure<IngressBindingOptions>(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, IOptions<IngressBindingOptions> bindings)
        {
            var logger = loggerFactory.CreateLogger("Ingress");
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                foreach (var mapping in bindings.Value.IpMappings)
                {
                    // endpoints.Map(mapping.Path, async context =>
                    // {
                    //     var client = new HttpClient();
                    //     var uri = $"http://{mapping.IpAddress}:{mapping.Port}{context.Request.Path}";
                    //     Console.WriteLine(uri);

                    //     var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri));
                    //     var response = await client.SendAsync(request);

                    //     // TODO figure out allow and deny list for headers
                    //     // foreach (var header in response.Headers)
                    //     // {
                    //     //     context.Response.Headers.Add(header.Key, header.Value.ToArray());
                    //     // }
                    //     await response.Content.CopyToAsync(context.Response.Body);
                    //     // foreach (var header in response.TrailingHeaders)
                    //     // {
                    //     //     context.Response.AppendTrailer(header.Key, header.Value.ToArray());
                    //     // }
                    // });

                    endpoints.Map(mapping.Path, async context =>
                    {
                        var client = new ClientBuilder(app.ApplicationServices).UseSockets().UseConnectionLogging().Build();
                        await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Parse(mapping.IpAddress), mapping.Port));
                        var httpProtocol = new HttpClientProtocol(connection);
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Request.Path);
                        var response = await httpProtocol.SendAsync(request);
                        logger.LogInformation(response.IsSuccessStatusCode.ToString());
                        logger.LogInformation(await response.Content.ReadAsStringAsync());
                        await response.Content.CopyToAsync(context.Response.Body);
                    });
                }
            });
        }
    }
}
