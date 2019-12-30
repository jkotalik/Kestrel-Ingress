using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ingress
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var client = new HttpClient();
                    // TODO need to get name and port from configuration.
                    var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
                    var response = await client.SendAsync(request);

                    foreach (var header in response.Headers)
                    {
                        context.Response.Headers.Add(header.Key, header.Value.ToArray());
                    }

                    await response.Content.CopyToAsync(context.Response.Body);

                    foreach (var header in response.TrailingHeaders)
                    {
                        context.Response.AppendTrailer(header.Key, header.Value.ToArray());
                    }
                });
            });
        }
    }
}
