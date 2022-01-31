using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace lroDemo
{
    public static class QueueTriggers
    {
        [FunctionName(nameof(QueueTriggerAsync))]
        public static async Task QueueTriggerAsync([ServiceBusTrigger(
            "startprocesstopic", "startprocess_sub", Connection = "ServiceBusConnection")]
            string myQueueItem,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger _logger)
        {
            Dictionary<string, string> values = null;
            _logger.LogInformation("Step 1: New message received from service bus = {myQueueItem}", myQueueItem);

            try
            {
                values = JsonConvert.DeserializeObject<Dictionary<string, string>>(myQueueItem);
            }
            catch (Exception e)
            {
                _logger.LogError("Step 1: Failed while parsing the input with = {message}", e.Message);
                return;
            }

            if (values.TryGetValue("operationId", out string outputOperationId))
            {
                string instanceId = await starter.StartNewAsync<Dictionary<string, string>>(
                    nameof(OrchestratorFunctions.StartOrchestrator), outputOperationId, values);
                _logger.LogInformation("Step 1: Orchestration triggered with instance ID = {instanceId}", instanceId);
            }
            else
            {
                _logger.LogError("Step 1: Unable to start the orchestration due to invalid operationId = {outputOperationId}", outputOperationId);
            }
        }

        [FunctionName(nameof(GetUvpResponse))]
        public static async Task GetUvpResponse([ServiceBusTrigger("uvpresponsetopic", "uvpresponse_sub", Connection = "ServiceBusConnection")] string myQueueItem,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger _logger)
        {

            _logger.LogInformation("Step 2: ServiceBus (UVPResponseTopic) function processed message = {myQueueItem}", myQueueItem);
            Dictionary<string, string> values = null;

            try
            {
                values = JsonConvert.DeserializeObject<Dictionary<string, string>>(myQueueItem);
            }
            catch (Exception)
            {
                _logger.LogError("Step 2: Unable to parse the Input = {myQueueItem}", myQueueItem);
                return;
            }

            if (values.TryGetValue("operationId", out string outputOperationId))
            {
                _logger.LogInformation("Step 2: Raising Event for UVP response with Instance ID = {outputOperationId} ", outputOperationId);
                _logger.LogInformation("Step 2: Adding 30 sec delay");
                Thread.Sleep(TimeSpan.FromSeconds(30));
                _logger.LogInformation("Step 2: Continue..");
                await starter.RaiseEventAsync(outputOperationId, nameof(QueueTriggers.GetUvpResponse), values);
                _logger.LogInformation("Step 2: Raised event to orchestrator");
            }
        }
    }
}