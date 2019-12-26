using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Hosting;

namespace Ingress.Controller
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    var config = KubernetesClientConfiguration.BuildDefaultConfig();
                    services.AddSingleton(config);
                    // Ideally this config would be read from the .net core config constructs,
                    // but that has not been implemented in the KubernetesClient library at
                    // the time this sample was created.
                    // Add the class that uses the client
                    services.AddHostedService<IngressHostedService>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
