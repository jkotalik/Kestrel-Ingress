using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using k8s;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BackendApp
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
                    await context.Response.WriteAsync("Hello from backend!");
                });
                endpoints.MapGet("/env", async context =>
                {
                    context.Response.ContentType = "application/json";
                    var configuration = context.RequestServices.GetRequiredService<IConfiguration>() as IConfigurationRoot;
                    var vars = Environment.GetEnvironmentVariables()
                                        .Cast<DictionaryEntry>()
                                        .OrderBy(e => (string)e.Key)
                                        .ToDictionary(e => (string)e.Key, e => (string)e.Value);
                    var data = new
                    {
                        version = Environment.Version.ToString(),
                        env = vars,
                        configuration = configuration.AsEnumerable().ToDictionary(c => c.Key, c => c.Value),
                        configurtionDebug = configuration.GetDebugView(),
                    };
                    await JsonSerializer.SerializeAsync(context.Response.Body, data);
                });

                endpoints.MapGet("/replicas", async context =>
                {
                    context.Response.ContentType = "application/json";

                    if (!KubernetesClientConfiguration.IsInCluster())
                    {
                        await JsonSerializer.SerializeAsync(context.Response.Body, new { message = "Not running in k8s" });
                        return;
                    }

                    var config = KubernetesClientConfiguration.InClusterConfig();
                    var klient = new Kubernetes(config);
                    var endpointsList = await klient.ListNamespacedEndpointsAsync("default");

                    await JsonSerializer.SerializeAsync(context.Response.Body, endpointsList.Items);
                });
            });
        }
    }
}
