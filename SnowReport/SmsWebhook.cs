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
using SnowReport.Messaging;
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


            var bodyParams = await GetBodyParams(req.Body);
            var bodyText = bodyParams["Body"];
            var from = bodyParams["From"];

            // Add incoming number to white list table
            if (bodyText.ToUpper() == "RIDE") return await StartUpdates(smsNumberTable, snowReportTable, from);

            if (bodyText == "STOP") return await StopUpdates(smsNumberTable, from);


            return new OkResult();
            
        }
        
        // TODO: Refactor to return a class
        private static async Task<Dictionary<string, string>> GetBodyParams(Stream body)
        {
            var requestBody = await new StreamReader(body).ReadToEndAsync();

            var decodedBody = Uri.UnescapeDataString(requestBody);

            var paramTokens = decodedBody.Split("&");
            var bodyParams = new Dictionary<string, string>();
            
            foreach (var token in paramTokens)
            {
                var parts = token.Split("=");
                bodyParams.Add(parts[0], parts[1]);
            }

            return bodyParams;
        }

        private static async Task SendLatestUpdate(CloudTable snowReportTable, string smsNumber)
        {
            var latestUpdateQuery = new TableQuery<SnowmobileReportEntity>()
                .Take(1);

            var queryResults = await snowReportTable.ExecuteQuerySegmentedAsync(latestUpdateQuery, null);
            var latestReport = queryResults.Results.First();
            await SmsMessaging.SendReport(smsNumber, latestReport);
        }

        private static async Task<ActionResult> StartUpdates(CloudTable smsNumberTable, CloudTable snowReportTable, string phoneNumber)
        {
            var query = new TableQuery<SmsNumberEntity>()
                .Where(TableQuery.GenerateFilterCondition("PhoneNumber",
                    QueryComparisons.Equal, phoneNumber));
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
                    PhoneNumber = phoneNumber,
                    Status = "Active"
                };

                var operation = TableOperation.Insert(incomingNumber);
                await smsNumberTable.ExecuteAsync(operation);
            }

            await SendLatestUpdate(snowReportTable, phoneNumber);
            
            return new OkResult();
        }

        private static async Task<ActionResult> StopUpdates(CloudTable smsNumberTable, string phoneNumber)
        {
            var query = new TableQuery<SmsNumberEntity>()
                .Where(TableQuery.GenerateFilterCondition("PhoneNumber", 
                    QueryComparisons.Equal, phoneNumber));

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

            return new OkResult();
        }

    }
}