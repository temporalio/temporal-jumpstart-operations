using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;

namespace Temporal.Operations.Proxy.Services;

public class TemporalHealthCheck : IHealthCheck
{
    // private readonly TemporalClientManager _clientManager;

    // public TemporalHealthCheck(TemporalClientManager clientManager)
    // {
    //     _clientManager = clientManager;
    // }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return new HealthCheckResult(HealthStatus.Healthy);
        // var namespaces = _clientManager.GetAvailableNamespaces().ToList();
        //
        // if (!namespaces.Any())
        // {
        //     return HealthCheckResult.Unhealthy("No Temporal namespaces configured");
        // }
        //
        // var healthyCount = 0;
        // foreach (var namespaceName in namespaces)
        // {
        //     var clientInfo = _clientManager.GetClientInfo(namespaceName);
        //     if (clientInfo != null)
        //     {
        //         try
        //         {
        //             var request = new GetSystemInfoRequest();
        //             var opts = new RpcOptions
        //             {
        //                 CancellationToken = cancellationToken,
        //             };
        //             await clientInfo.WorkflowServiceClient.GetSystemInfoAsync(request,opts);
        //             healthyCount++;
        //         }
        //         catch
        //         {
        //             // Connection failed
        //         }
        //     }
        // }
        //
        // var healthRatio = (double)healthyCount / namespaces.Count;
        // return healthRatio >= 0.5 
        //     ? HealthCheckResult.Healthy($"{healthyCount}/{namespaces.Count} namespaces healthy")
        //     : HealthCheckResult.Degraded($"Only {healthyCount}/{namespaces.Count} namespaces healthy");
    }
}