using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ingress.Controller
{
    internal class PodListHostedService : IHostedService
    {
        private readonly IKubernetes _kubernetesClient;
        private readonly ILogger<PodListHostedService> _logger;

        public PodListHostedService(IKubernetes kubernetesClient, ILogger<PodListHostedService> logger)
        {
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Request!");
            var list = await _kubernetesClient.ListNamespacedPodAsync("default", cancellationToken: cancellationToken);
            foreach (var item in list.Items)
            {
                _logger.LogInformation(item.Metadata.Name);
            }
            if (list.Items.Count == 0)
            {
                _logger.LogInformation("Empty!");
            }
            // From what I can tell, I need to listen for things that happen for the ingress resource, update a config file
            // that kestrel is using, optionally create the resource as well, and go from there?
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            return Task.CompletedTask;
        }
    }
}