using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SnowReport
{
    public static class SmsWebhook
    {
        [FunctionName("SmsRegister")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
            HttpRequest req, ILogger log, [Table("SnowReport")] CloudTable snowReportTable,
            [Table("SmsNumber")] CloudTable smsNumberTable)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var decodedBody = Uri.UnescapeDataString(requestBody);

            var paramTokens = decodedBody.Split("&");
            var bodyParams = new Dictionary<string, string>();
            

            foreach (var token in paramTokens)
            {
                var parts = token.Split("=");
                bodyParams.Add(parts[0], parts[1]);
            }

            // Add incoming number to white list table
            if (bodyParams["Body"].ToUpper() == "RIDE")
            {
                var rowKey = Utilities.GetNewRowKey();
                
                var query = new TableQuery<SmsNumberEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PhoneNumber",
                        QueryComparisons.Equal, bodyParams["From"]));
                var querySegments = await smsNumberTable.ExecuteQuerySegmentedAsync(query, null);
                var results = querySegments.Results;

                if (results.Any())
                {
                    var row = results.First();
                    row.Status = "Active";
                    var tableOp = TableOperation.Replace(row);
                    await smsNumberTable.ExecuteAsync(tableOp);
                }
                else
                {
                    var incomingNumber = new SmsNumberEntity()
                    {
                        PartitionKey = "Version 1",
                        RowKey = Utilities.GetNewRowKey(),
                        PhoneNumber = bodyParams["From"],
                        Status = "Active"
                    };

                    var operation = TableOperation.Insert(incomingNumber);
                    await smsNumberTable.ExecuteAsync(operation);
                }

                await SendLatestUpdate(snowReportTable, bodyParams["From"]);
            }

            if (bodyParams["Body"] == "STOP")
            {
                var query = new TableQuery<SmsNumberEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PhoneNumber", 
                        QueryComparisons.Equal, bodyParams["From"]));

                var results = await smsNumberTable.ExecuteQuerySegmentedAsync(query, null);
                
                if (!results.Results.Any())
                {
                    return new OkResult();
                }
                
                // Set status to stopped in table
                var match = results.Results.First();

                match.Status = "Inactive";

                var operation = TableOperation.Replace(match);
                await smsNumberTable.ExecuteAsync(operation);
            }

            return new OkResult();
            
        }

        public static async Task SendLatestUpdate(CloudTable snowReportTable, string smsNumber)
        {
            var latestUpdateQuery = new TableQuery<SnowmobileReportEntity>()
                .Take(1);

            var queryResults = await snowReportTable.ExecuteQuerySegmentedAsync(latestUpdateQuery, null);
            var latestReport = queryResults.Results.First();
                        
            var accountSid = Environment.GetEnvironmentVariable("TwilioAccountSid");
            var authToken = Environment.GetEnvironmentVariable("TwilioAuthToken");

            TwilioClient.Init(accountSid, authToken);


            MessageResource.Create(
                body: $"Snow Report Update\n\n" +
                      $"Updated: {latestReport.ModifiedDate}\n" +
                      $"Base: {latestReport.Base}\n" +
                      $"Groomed: {latestReport.Groomed}\n" +
                      $"Condition: {latestReport.Condition}\n\n" +
                      $"{latestReport.Description}",
                from: new Twilio.Types.PhoneNumber("+13852357816"),
                to: new Twilio.Types.PhoneNumber(smsNumber)
            );

        }

    }
}