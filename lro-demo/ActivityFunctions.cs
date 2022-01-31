using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace lroDemo
{
    public static class ActivityFunctions
    {

        [FunctionName(nameof(SubmitReuqestToUVPAsync))]
        [return: ServiceBus("uvpresponsetopic", Connection = "ServiceBusConnection")]
        public static async Task<string> SubmitReuqestToUVPAsync([ActivityTrigger] Dictionary<string, string> request,
            ILogger log)
        {
            //make activity function idempotent
            request["operationStatus"] = "Validated";
            var response  = JsonConvert.SerializeObject(request);
            log.LogInformation("Step 2: Submitting UVP response to ServiceBus {response}", response);
            await Task.Delay(TimeSpan.FromSeconds(5));
            return response;
        }

        [FunctionName(nameof(ProcessStepFour))]
        public static async Task<bool> ProcessStepFour([ActivityTrigger] string request,
            ILogger log)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            log.LogInformation("Step 4: Processing...= {request} ", request);
            return true;
        }

        [FunctionName(nameof(ProcessStepFive))]
        public static async Task<bool> ProcessStepFive([ActivityTrigger] string request,
            ILogger log)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            log.LogInformation("Step 5: Processing...= {request} ", request);
            return true;
        }

        [FunctionName(nameof(ProcessStepSix))]
        public static async Task ProcessStepSix([ActivityTrigger] Dictionary<string, string> request,
            ILogger log)
        {
            bool x = bool.Parse(request["isFailedWithRetryRequested"]);
            if (x)
            {
                log.LogInformation("Step 6: failure requested for instance Id= {request} ", request["instanceId"]);
                throw new InvalidOperationException("error"); 
            }
            await Task.Delay(TimeSpan.FromSeconds(5));
            log.LogInformation("Step 6: Submitting request to publishing service = {request} ", request["instanceId"]);
        }

        [FunctionName(nameof(ProcessStepSeven))]
        public static async Task ProcessStepSeven([ActivityTrigger] string request,
            ILogger log)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            log.LogInformation("Step 7: Sending email to user = {request} ", request);
        }
    }
}