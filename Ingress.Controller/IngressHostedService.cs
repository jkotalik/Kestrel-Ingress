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
using System.Collections.Generic;
using System.Linq;
using Ingress.Library;

namespace Ingress.Controller
{
    internal class IngressHostedService : IHostedService
    {
        private readonly KubernetesClientConfiguration _config;
        private readonly ILogger<IngressHostedService> _logger;
        private Watcher<Extensionsv1beta1Ingress> _watcher;
        private Process _process;
        private Kubernetes _klient;

        public IngressHostedService(KubernetesClientConfiguration config, ILogger<IngressHostedService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started ingress hosted service");
            _klient = new Kubernetes(_config);
            var result = _klient.ListNamespacedIngressWithHttpMessagesAsync("default", watch: true);
            _watcher = result.Watch((Action<WatchEventType, Extensionsv1beta1Ingress>)(async (type, item) =>
            {
                _logger.LogInformation("Got an event for ingress!");
                _logger.LogInformation(item.Metadata.Name);
                _logger.LogInformation(item.Kind);
                _logger.LogInformation(type.ToString());
                if (type == WatchEventType.Added)
                {
                    // Create a process to run the ingress, get port from stdout?
                    await CreateJsonBlob(item);
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

        private async ValueTask CreateJsonBlob(Extensionsv1beta1Ingress ingress)
        {
            // Get IP and port from k8s.
            var fileStream = File.Open("/app/Ingress/ingress.json", FileMode.CreateNew);
            var ipMappingList = new List<IpMapping>();
            if (ingress.Spec.Backend != null)
            {
                // TODO do same logic 
            }
            else
            {
                // TODO maybe check that a host is present:
                // An optional host. In this example, no host is specified, so the rule applies to all 
                // inbound HTTP traffic through the IP address specified. If a host is provided 
                // (for example, foo.bar.com), the rules apply to that host.
                foreach (var i in ingress.Spec.Rules)
                {
                    foreach (var path in i.Http.Paths)
                    {
                        var service = await _klient.ReadNamespacedServiceAsync(name: path.Backend.ServiceName, namespaceParameter: ingress.Metadata.NamespaceProperty);
                        ipMappingList.Add(new IpMapping { IpAddress = service.Spec.ClusterIP, Port = path.Backend.ServicePort, Path = path.Path });
                    }
                }
            }
            
            var json = new IngressBindingOptions() {IpMappings = ipMappingList};
            await JsonSerializer.SerializeAsync(fileStream, json, typeof(IngressBindingOptions));
            fileStream.Close();
        }
    }
}