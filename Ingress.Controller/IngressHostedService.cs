using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ingress.Controller
{
    internal class IngressHostedService : IHostedService
    {
        private readonly KubernetesClientConfiguration _config;
        private readonly ILogger<IngressHostedService> _logger;
        private Watcher<Extensionsv1beta1Ingress> _watcher;

        public IngressHostedService(KubernetesClientConfiguration config, ILogger<IngressHostedService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started ingress hosted service");
            var klient = new Kubernetes(_config);
            var result = klient.ListNamespacedIngressWithHttpMessagesAsync("default", watch: true);
            _watcher = result.Watch<Extensionsv1beta1Ingress, Extensionsv1beta1IngressList>((type, item) =>
            {
                _logger.LogInformation("Got an event for ingress!");
                _logger.LogInformation(item.Metadata.Name);
                _logger.LogInformation(type.ToString());
                if (type == WatchEventType.Added)
                {
                    // Do something to add here.
                }
                else if (type == WatchEventType.Deleted)
                {

                }
                else if (type == WatchEventType.Modified)
                {

                }
                else
                {

                }
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            _watcher.Dispose();
            return Task.CompletedTask;
        }
    }
}