using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ingress.Controller
{
    public class Startup
    {
        private ILogger _logger;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory factory)
        {
            _logger = factory.CreateLogger("startup");
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
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
                    foreach (var eps in endpointsList.Items)
                    {
                        await JsonSerializer.SerializeAsync(context.Response.Body, eps.Metadata);
                    }
                });

                endpoints.MapGet("/ingress", async context =>
                {
                    context.Response.ContentType = "application/json";
                    var tcs = new TaskCompletionSource<object>();
                    var config = KubernetesClientConfiguration.InClusterConfig();
                    var klient = new Kubernetes(config);
                    var result = klient.ListNamespacedIngressWithHttpMessagesAsync("default", watch: true);
                    string name = "";
                    using (result.Watch<Extensionsv1beta1Ingress, Extensionsv1beta1IngressList>((type, item) =>
                    {
                        _logger.LogInformation("Got an event for ingress!");
                        _logger.LogInformation(item.Metadata.Name);
                        name = item.Metadata.Name;
                        tcs.SetResult(null);
                    }))
                    {
                        await tcs.Task;
                        await context.Response.WriteAsync(name);
                    }
                });
            });
        }
    }
}
