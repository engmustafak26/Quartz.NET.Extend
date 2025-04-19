using System;

namespace Quartz.NET.Extend
{
    internal class JobConfigurationModel
    {
        public TimeSpan Delay { get; set; }
        public TimeSpan? RecurringInterval { get; set; }
        public int? RepeatCount { get; set; }
        public string CronExpression { get; set; }

        public bool IsCron => RecurringInterval == null;
    }
}
