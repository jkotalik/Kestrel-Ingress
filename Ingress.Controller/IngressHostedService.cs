using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ingress.Controller
{
    internal class IngressHostedService : IHostedService
    {
        private readonly KubernetesClientConfiguration _config;
        private readonly ILogger<IngressHostedService> _logger;
        private Watcher<Extensionsv1beta1Ingress> _watcher;
        private Process _process;

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
            _watcher = result.Watch((Action<WatchEventType, Extensionsv1beta1Ingress>)((type, item) =>
            {
                _logger.LogInformation("Got an event for ingress!");
                _logger.LogInformation(item.Metadata.Name);
                _logger.LogInformation(item.Kind);
                _logger.LogInformation(type.ToString());
                if (type == WatchEventType.Added)
                {
                    // Create a process to run the ingress, get port from stdout?
                    CreateJsonBlob(item);
                    StartProcess();
                }
                else if (type == WatchEventType.Deleted)
                {
                    _process.Close();
                }
                else if (type == WatchEventType.Modified)
                {
                    // Generate a new configuration here and let the process handle it.
                    _process.Close();
                    StartProcess();
                }
                else
                {
                    // Error, close the process?
                }
            }));
            return Task.CompletedTask;
        }

        private void StartProcess()
        {
            _process = new Process();
            _logger.LogInformation(File.Exists("/app/Ingress/Ingress.dll").ToString());
            var startInfo = new ProcessStartInfo("dotnet", "/app/Ingress/Ingress.dll");
            startInfo.WorkingDirectory = "/app/Ingress";
            startInfo.CreateNoWindow = true;
            _process.StartInfo = startInfo;
            _process.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop
            _watcher.Dispose();
            return Task.CompletedTask;
        }

        private void CreateJsonBlob(Extensionsv1beta1Ingress ingress)
        {
            var ingressConfig = JsonSerializer.Serialize(ingress, typeof(Extensionsv1beta1Ingress));
            File.WriteAllText("/app/Ingress/ingress.json", ingressConfig);
        }
    }
}