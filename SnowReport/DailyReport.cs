using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using RestSharp;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SnowReport
{
    public static class DailyReport
    {
        [FunctionName("DailyReport")]
        [return: Table("SnowReport")]
        public static async Task<SnowmobileReportEntity> RunAsync([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log,
            [Table("SnowReport")] CloudTable snowReportTable, [Table("SmsNumber")] CloudTable smsNumberTable)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
            var accountSid = Environment.GetEnvironmentVariable("TwilioAccountSid");
            var authToken = Environment.GetEnvironmentVariable("TwilioAuthToken");

            var client = new RestClient("https://www.travelwisconsin.com/snowreport/reportdetails");
            var request = new RestRequest(Method.POST);
            request.AddParameter("id", "1671");
            request.AddParameter("reportType", "snowmobile");
            var response = await client.ExecuteTaskAsync<SnowmobileReportEntity>(request);
            
            var lastReportQuery = new TableQuery<SnowmobileReportEntity>()
                .Where(TableQuery.GenerateFilterCondition("ModifiedDate", 
                    QueryComparisons.Equal,
                    response.Data.ModifiedDate));

            var queryResult = await snowReportTable.ExecuteQuerySegmentedAsync(lastReportQuery, null);


            if (queryResult.Results.Any())
            {
                log.LogInformation("No changes.");
                return null;
            } 

            log.LogInformation("Sending update via SMS...");
            TwilioClient.Init(accountSid, authToken);

            var smsNumberQuery = new TableQuery<SmsNumberEntity>()
                .Where(TableQuery.GenerateFilterCondition("Status", QueryComparisons.Equal, "Active"));

            var smsNumberResults = await smsNumberTable.ExecuteQuerySegmentedAsync(smsNumberQuery, null);

            var smsNumbers = smsNumberResults.Results;

            foreach (var smsNumber in smsNumbers)
            {
                log.LogInformation($"Sending update to {smsNumber.PhoneNumber}");
                
                var message = MessageResource.Create(
                    body: $"Snow Report Update\n\n" +
                          $"Updated: {response.Data.ModifiedDate}\n" +
                          $"Base: {response.Data.Base}\n" +
                          $"Groomed: {response.Data.Groomed}\n" +
                          $"Condition: {response.Data.Condition}\n\n" +
                          $"{response.Data.Description}",
                    from: new Twilio.Types.PhoneNumber("+13852357816"),
                    to: new Twilio.Types.PhoneNumber(smsNumber.PhoneNumber)
                );
            }

            log.LogInformation("Done.");

            response.Data.RowKey = Utilities.GetNewRowKey();
            response.Data.PartitionKey = "Version 1";
            return response.Data;
            
        }
    }
}