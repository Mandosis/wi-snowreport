using Microsoft.WindowsAzure.Storage.Table;

namespace SnowReport
{
    public class SmsNumberEntity: TableEntity
    {
        public string PhoneNumber { get; set; }
        public string Status { get; set; }
    }
}