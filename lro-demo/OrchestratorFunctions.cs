using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace lroDemo
{
    public static class OrchestratorFunctions
    {
        [FunctionName(nameof(StartOrchestrator))]
        public static async Task<object> StartOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger _logger)
        {
            _logger = context.CreateReplaySafeLogger(_logger);
            Dictionary<string, string> request = context.GetInput<Dictionary<string, string>>();
            _logger.LogInformation("Step 2: Submitting request to UVP system with instance ID  = {instanceId}", context.InstanceId);
            
            await context.CallActivityWithRetryAsync<string>(nameof(ActivityFunctions.SubmitReuqestToUVPAsync),new RetryOptions(
                TimeSpan.FromSeconds(10), maxNumberOfAttempts:3){RetryTimeout= TimeSpan.FromSeconds(100)}, request);

            Dictionary<string, string> uvpResponse = null;
            try
            {
                _logger.LogInformation("Step 2: Waiting for UVP response from Service bus for instance ID  = {instanceId}", context.InstanceId);
                uvpResponse = await context.WaitForExternalEvent<Dictionary<string, string>>(
                    nameof(QueueTriggers.GetUvpResponse), TimeSpan.FromMinutes(2));
            }
            catch (TimeoutException)
            {
                _logger.LogInformation("Step 2: Request Timeout : UVP response not received within 2 minutes for instance ID = {instanceId}", context.InstanceId);
                return new
                {
                    Response = "Request timed out for the request Id : " + context.InstanceId,
                };
            }

            _logger.LogInformation("Step 2: Response received from UVP is = {status} for instance ID = {instanceId}", uvpResponse["operationStatus"], context.InstanceId);

            _logger.LogInformation("Step 3: Executing Step 4 & 5 in parallel for Instance Id = {instanceId}", context.InstanceId);

            var parallelTasks = new List<Task<bool>>();

            Task<bool> task1 = context.CallActivityWithRetryAsync<bool>(nameof(ActivityFunctions.ProcessStepFour), new RetryOptions(
            TimeSpan.FromSeconds(10), maxNumberOfAttempts: 3){ RetryTimeout = TimeSpan.FromSeconds(60) }, context.InstanceId);

            Task<bool> task2 = context.CallActivityWithRetryAsync<bool>(nameof(ActivityFunctions.ProcessStepFive), new RetryOptions(
            TimeSpan.FromSeconds(20), maxNumberOfAttempts: 2){ RetryTimeout = TimeSpan.FromSeconds(60) }, context.InstanceId);

            parallelTasks.Add(task1);
            parallelTasks.Add(task2);
            await Task.WhenAll(parallelTasks);

            bool output = true;

            foreach (Task<bool> parallelOutput in parallelTasks){
                output &= parallelOutput.Result;
            }

            /// test case to fail orchestrator
            if (bool.Parse(uvpResponse["throwException"]))
            {
                throw new Exception("throwing exception manually");
            }

            output = !bool.Parse(uvpResponse["isToggleStepRequested"]);
            _logger.LogInformation("Step 3: Response received from step 4 & 5 for Instance Id = {instanceId} is = {output}", context.InstanceId, output);
            if (output)
            {
                _logger.LogInformation("Step 6 : Starting for instance ID = {instanceId}", context.InstanceId);

                Dictionary<string, string> input = new Dictionary<string, string>();
                input.Add("instanceId", context.InstanceId);
                input.Add("isFailedWithRetryRequested", uvpResponse["isFailedWithRetryRequested"]);

                await context.CallActivityWithRetryAsync(nameof(ActivityFunctions.ProcessStepSix), new RetryOptions(
                    TimeSpan.FromSeconds(10), maxNumberOfAttempts: 2) { RetryTimeout = TimeSpan.FromSeconds(30) },
                    input);

                _logger.LogInformation("Step 6 : Completed for instance ID = {instanceId}", context.InstanceId);
            }
            else
            {
                _logger.LogInformation("Step 7 : Starting for instance ID = {instanceId}", context.InstanceId);
                await context.CallActivityAsync(nameof(ActivityFunctions.ProcessStepSeven), context.InstanceId);
                _logger.LogInformation("Step 7 : Completed for instance ID = {instanceId}", context.InstanceId);
            }

            _logger.LogInformation("Step 8 : All steps completed for instance ID = {instanceId}", context.InstanceId);

            return new {
                Response = "Request has been completed for the request Id : " + context.InstanceId
            };
        }
    }
}