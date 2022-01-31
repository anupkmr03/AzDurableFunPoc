using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading;
using System.Collections.Generic;
using System.Net.Http;

namespace lroDemo
{
    public static class GeneralFunctions
    {

        [FunctionName(nameof(TerminateInstance))]
        public static async Task<IActionResult> TerminateInstance(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Terminate/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation("Terminating instance ID = {instanceId}", instanceId);
            string reason = "Operation has been terminated manually";
            await client.TerminateAsync(instanceId, reason);
            return new OkResult();
        }

        [FunctionName(nameof(RestartInstance))]
        public static async Task<IActionResult> RestartInstance(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Restart/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation("Restarting instance ID = {instanceId}", instanceId);
            await client.RestartAsync(instanceId, false);
            return new OkResult();
        }

        [FunctionName(nameof(PurgeInstanceHistory))]
        public static async Task<IActionResult> PurgeInstanceHistory(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "PurgeHistory/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation("Purging history for instance ID = {instanceId}", instanceId);
            var response = await client.PurgeInstanceHistoryAsync(instanceId);
            return new OkObjectResult(response);
        }

        [FunctionName(nameof(RewindInstance))]
        public static async Task<IActionResult> RewindInstance(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Rewind/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation("Restarting instance ID = {instanceId}", instanceId);

            string reason = "Operation has been re-winded externally";
            await client.RewindAsync(instanceId, reason);
            return new OkResult();
        }

        [FunctionName(nameof(GetStatus))]
        public static async Task<IActionResult> GetStatus(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Query/{instanceId}")] HttpRequest req,
            string instanceId,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation("Getting Status for instance ID = {instanceId}", instanceId);
            DurableOrchestrationStatus output = await client.GetStatusAsync(instanceId, showHistory: true, showHistoryOutput: true);
            return new OkObjectResult(output);
        }

        [FunctionName(nameof(GetAllStatus))]
        public static async Task<IActionResult> GetAllStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
        {
            var noFilter = new OrchestrationStatusQueryCondition();
            OrchestrationStatusQueryResult result = await client.ListInstancesAsync(
                noFilter,
                CancellationToken.None);

            var output = new List<DurableOrchestrationStatus>();
            foreach (DurableOrchestrationStatus instance in result.DurableOrchestrationState)
            {
                log.LogInformation(JsonConvert.SerializeObject(instance));
                output.Add(instance);
            }

            return new OkObjectResult(JsonConvert.SerializeObject(output));
        }

        [FunctionName(nameof(QueryPendingJobs))]
        public static async Task<IActionResult> QueryPendingJobs(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
        {
            // Get the first 100 running or pending instances that were created between 7 and 1 day(s) ago
            var queryFilter = new OrchestrationStatusQueryCondition
            {
                RuntimeStatus = new[]
                {
                    OrchestrationRuntimeStatus.Pending,
                    OrchestrationRuntimeStatus.Running,
                },
                CreatedTimeFrom = DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)),
                CreatedTimeTo = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                PageSize = 100,
            };

            OrchestrationStatusQueryResult result = await client.ListInstancesAsync(
                queryFilter,
                CancellationToken.None);

            var output = new List<DurableOrchestrationStatus>();
            foreach (DurableOrchestrationStatus instance in result.DurableOrchestrationState)
            {
                log.LogInformation(JsonConvert.SerializeObject(instance));
                output.Add(instance);
            }
            return new OkObjectResult(JsonConvert.SerializeObject(output));
        }
    }
}