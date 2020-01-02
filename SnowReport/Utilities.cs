using System;

namespace SnowReport
{
    public class Utilities
    {
        public static string GetNewRowKey()
        {
            return (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19");
        }
    }
}