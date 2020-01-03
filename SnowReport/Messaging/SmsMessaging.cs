using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SnowReport.Messaging
{
    public class SmsMessaging
    {
        public static async Task SendReport(string phoneNumber, SnowmobileReportEntity report)
        {
            // TODO: Get sending phone number from env. variable
            var accountSid = Utilities.GetEnvironmentVariable("TwilioAccountSid");
            var authToken = Utilities.GetEnvironmentVariable("TwilioAuthToken");
            var decodedDescription = System.Web.HttpUtility.HtmlDecode(report.Description);
            
            TwilioClient.Init(accountSid, authToken);
            
            await MessageResource.CreateAsync(
                body: $"Snow Report Update\n\n" +
                      $"Updated: {report.ModifiedDate}\n" +
                      $"Base: {report.Base}\n" +
                      $"Groomed: {report.Groomed}\n" +
                      $"Condition: {report.Condition}\n\n" +
                      $"{decodedDescription}",
                from: new Twilio.Types.PhoneNumber("+13852357816"),
                to: new Twilio.Types.PhoneNumber(phoneNumber)
            );
        }
    }
}