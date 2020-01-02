using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

namespace SnowReport
{
    public class SnowmobileReportEntity: TableEntity
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public List<object> Images { get; set; }
        public string Base { get; set; }
        public string Groomed { get; set; }
        public string Condition { get; set; }
        public string Url { get; set; }
        public string LocationUrl { get; set; }
        public string Description { get; set; }
        public string ModifiedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string LocationStatus { get; set; }
        public string ConditionClass { get; set; }
        public string City { get; set; }
        public string CountyID { get; set; }
        public string RegionID { get; set; }
    }
}