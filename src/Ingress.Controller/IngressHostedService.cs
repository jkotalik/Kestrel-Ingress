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
        private Watcher<V1Endpoints> _endpointWatcher;
        private Process _process;
        private Kubernetes _klient;
        private object _sync = new object();

        Dictionary<string, List<string>> _serviceToIp = new Dictionary<string, List<string>>();
        private Dictionary<string, IpMapping> _ipMappingList;
        private TaskCompletionSource<object> _tcs;

        public IngressHostedService(KubernetesClientConfiguration config, ILogger<IngressHostedService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ipMappingList = new Dictionary<string, IpMapping>();
            _tcs = new TaskCompletionSource<object>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started ingress hosted service.");
            try
            {
                _klient = new Kubernetes(_config);
                var result = _klient.ListNamespacedIngressWithHttpMessagesAsync("default", watch: true);

                _watcher = result.Watch((Action<WatchEventType, Extensionsv1beta1Ingress>)(async (type, item) =>
                {
                    if (type == WatchEventType.Added)
                    {
                        _logger.LogInformation("Added event");
                        await CreateJsonBlob(item);
                        StartProcess();
                    }
                    else if (type == WatchEventType.Modified)
                    {
                        // Generate a new configuration here and let the process handle it.
                        _logger.LogInformation("Modified event");
                        await CreateJsonBlob(item);
                    }
                    else if (type == WatchEventType.Deleted)
                    {
                        _logger.LogInformation("Deleted event");
                        _process.Close();
                    }
                    else
                    {
                    }
                }));

                var result2 = _klient.ListNamespacedEndpointsWithHttpMessagesAsync("default", watch: true);
                _endpointWatcher = result2.Watch((Action<WatchEventType, V1Endpoints>)(async (type, item) =>
                {
                    _logger.LogInformation($"Got endpoints {type.ToString()}");

                    if (type == WatchEventType.Added)
                    {
                        HandleAddForEndpoint(item);
                        await UpdateJsonBlob(item.Metadata.Name);
                    }
                    else if (type == WatchEventType.Modified)
                    {
                        HandleAddForEndpoint(item);
                        await UpdateJsonBlob(item.Metadata.Name);
                    }
                    else if (type == WatchEventType.Deleted)
                    {
                        // remove from dictionary.
                        HandleDeleteForEndpoint(item);
                    }
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return Task.CompletedTask;
        }

        private async Task UpdateJsonBlob(string serviceName)
        {
            bool shouldWrite;
            lock (_sync)
            {
                shouldWrite = _ipMappingList.ContainsKey(serviceName);
                if (shouldWrite)
                {
                    _ipMappingList[serviceName].IpAddresses = _serviceToIp[serviceName];
                }
            }

            if (shouldWrite)
            {
                await _tcs.Task;
                var ingressFile = "/app/Ingress/ingress.json";
                var fileStream = File.Open(ingressFile, FileMode.Create);
                var json = new IngressBindingOptions() {IpMappings = _ipMappingList.Values.ToList()};
                await JsonSerializer.SerializeAsync(fileStream, json, typeof(IngressBindingOptions));
                fileStream.Close();
            }
        }

        private void HandleAddForEndpoint(V1Endpoints endpoint)
        {
            lock (_sync)
            {
                if (endpoint != null && endpoint.Subsets != null)
                {
                    _serviceToIp[endpoint.Metadata.Name] = endpoint.Subsets.SelectMany((o) => o.Addresses).Select(a => a.Ip).ToList();
                    _logger.LogInformation($"Actually updating endpoint {endpoint.Metadata.Name}");
                    foreach(var s in _serviceToIp[endpoint.Metadata.Name])
                    {
                        _logger.LogInformation($"Endpoint: {s}");
                    }
                }
            }
        }

        private void HandleDeleteForEndpoint(V1Endpoints endpoint)
        {
            lock (_sync)
            {
                _logger.LogInformation(endpoint.Kind.ToString());
                _logger.LogInformation(endpoint.Metadata.ToString());
                _logger.LogInformation(endpoint.ToString());
                if (endpoint != null && endpoint.Subsets != null)
                {
                    _serviceToIp.Remove(endpoint.Metadata.Name);
                    _logger.LogInformation($"Actually removing endpoint {endpoint.Metadata.Name}");
                }
            }
        }

        private void UpdateServiceToEndpointDictionary(V1EndpointsList item)
        {
            lock (_sync)
            {
                _logger.LogInformation(item.Kind.ToString());
                _logger.LogInformation(item.Metadata.ToString());
                _logger.LogInformation(item.ToString());
                if (item != null && item.Items != null)
                {
                    _logger.LogInformation($"Items aren't null");
                    foreach (var endpoint in item.Items)
                    {
                        _logger.LogInformation($"Checking endpoint");
                        if (endpoint != null && endpoint.Subsets != null)
                        {
                            _serviceToIp[endpoint.Metadata.Name] = endpoint.Subsets.SelectMany((o) => o.Addresses).Select(a => a.Ip).ToList();
                            _logger.LogInformation($"Actually updating endpoint {endpoint.Metadata.Name} {_serviceToIp[endpoint.Metadata.Name].ToString()}");
                        }
                    }
                }
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

            var fileStream = File.Open(ingressFile, FileMode.Create);
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
                        _logger.LogInformation(path.Backend.ServiceName);
                    }

                    if (exists)
                    {
                        _logger.LogInformation("IP mapping exists, use it.");
                        lock(_sync)
                        {
                            _ipMappingList[path.Backend.ServiceName] = new IpMapping { IpAddresses = ipList, Port = path.Backend.ServicePort, Path = path.Path };
                        }
                    }
                    else
                    {
                        _logger.LogInformation("querying for endpoints");
                        var endpoints = await _klient.ListNamespacedEndpointsAsync(namespaceParameter: ingress.Metadata.NamespaceProperty);
                        var service = await _klient.ReadNamespacedServiceAsync(path.Backend.ServiceName, ingress.Metadata.NamespaceProperty);
                        
                        // TODO can there be multiple ports here?
                        var targetPort = service.Spec.Ports.Where(e => e.Port == path.Backend.ServicePort).Select(e => e.TargetPort).Single();

                        UpdateServiceToEndpointDictionary(endpoints);
                        lock(_sync)
                        {
                            // From what it looks like, scheme is always http unless the tls section is specified, 
                            _ipMappingList[path.Backend.ServiceName] = new IpMapping { IpAddresses = _serviceToIp[path.Backend.ServiceName], Port = targetPort, Path = path.Path, Scheme = "http" };
                        }
                    }
                }
            }
            
            var json = new IngressBindingOptions() {IpMappings = _ipMappingList.Values.ToList()};
            await JsonSerializer.SerializeAsync(fileStream, json, typeof(IngressBindingOptions));
            fileStream.Close();
            
            _tcs.SetResult(null);
        }
    }
}