using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Ingress.Controller
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            using (var host = new HostBuilder()
                .ConfigureLogging((logging) =>
                {
                    logging.AddConsole();
                })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    // Ideally this config would be read from the .net core config constructs,
                    // but that has not been implemented in the KubernetesClient library at
                    // the time this sample was created.
                    var config = KubernetesClientConfiguration.BuildDefaultConfig();
                    services.AddSingleton(config);

                    // Setup the http client
                    services.AddHttpClient("K8s")
                        .AddTypedClient<IKubernetes>((httpClient, serviceProvider) =>
                        {
                            return new Kubernetes(
                                serviceProvider.GetRequiredService<KubernetesClientConfiguration>(),
                                httpClient);
                        });

                    // Add the class that uses the client
                    services.AddHostedService<PodListHostedService>();
                })
                .Build())
            {
                await host.StartAsync().ConfigureAwait(false);
                await host.StopAsync().ConfigureAwait(false);
            }
        }
    }
}
