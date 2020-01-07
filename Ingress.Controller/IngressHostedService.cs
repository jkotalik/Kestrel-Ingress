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
using System.Net;

namespace Ingress.Controller
{
    internal class IngressHostedService : IHostedService
    {
        private readonly KubernetesClientConfiguration _config;
        private readonly ILogger<IngressHostedService> _logger;
        private Watcher<Extensionsv1beta1Ingress> _watcher;
        private Watcher<V1EndpointsList> _endpointWatcher;
        private Process _process;
        private Kubernetes _klient;
        private object _sync = new object();

        Dictionary<string, List<string>> _serviceToIp = new Dictionary<string, List<string>>();
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
                // TODO move logic out of watch callback.
                if (type == WatchEventType.Added)
                {
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

            var result2 = _klient.ListNamespacedEndpointsWithHttpMessagesAsync("default", watch: true);
            _endpointWatcher = result2.Watch((Action<WatchEventType, V1EndpointsList>)((type, item) =>
            {
                if (type == WatchEventType.Added)
                {
                    UpdateServiceToEndpointDictionary(item);
                }
                // TODO do I need this lock?

            }));
            return Task.CompletedTask;
        }

        private void UpdateServiceToEndpointDictionary(V1EndpointsList item)
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var endpoint in item.Items)
            {
                dict[endpoint.Metadata.Name] = endpoint.Subsets.SelectMany((o) => o.Addresses).Select(a => a.Ip).ToList();
            }
            lock (_sync)
            {
                _serviceToIp = dict;
            }
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
            var ingressFile = "/app/Ingress/ingress.json";
            if (File.Exists(ingressFile))
            {
                File.Delete(ingressFile);
            }
            var fileStream = File.Open(ingressFile, FileMode.CreateNew);
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
                        bool exists;
                        List<string> ipList;

                        lock (_sync)
                        {
                            exists = _serviceToIp.TryGetValue(path.Backend.ServiceName, out ipList);
                        }

                        if (exists)
                        {
                            ipMappingList.Add(new IpMapping { IpAddresses = ipList, Port = path.Backend.ServicePort, Path = path.Path });
                        }
                        else
                        {
                            // var service = await _klient.ReadNamespacedServiceAsync(name: path.Backend.ServiceName, namespaceParameter: ingress.Metadata.NamespaceProperty);
                            _logger.LogInformation("Getting endpoints");
                            // This needs to filter down for endpoints that match the service?
                            var endpoints = await _klient.ListNamespacedEndpointsAsync(namespaceParameter: ingress.Metadata.NamespaceProperty);
                            var service = await _klient.ReadNamespacedServiceAsync(path.Backend.ServiceName, ingress.Metadata.NamespaceProperty);
                            
                            // TODO can there be multiple ports here?
                            var targetPort = service.Spec.Ports.Where(e => e.Port == path.Backend.ServicePort).Select(e => e.TargetPort).Single();
                            // need to find the mapping from servicePort to targetPort.
                            UpdateServiceToEndpointDictionary(endpoints);
                            lock(_sync)
                            {
                                ipMappingList.Add(new IpMapping { IpAddresses = _serviceToIp[path.Backend.ServiceName], Port = targetPort, Path = path.Path, Scheme = "http" });
                            }
                        }
                    }
                }
            }
            
            var json = new IngressBindingOptions() {IpMappings = ipMappingList};
            await JsonSerializer.SerializeAsync(fileStream, json, typeof(IngressBindingOptions));
            fileStream.Close();
        }
    }
}