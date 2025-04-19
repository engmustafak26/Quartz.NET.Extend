using global::Quartz;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Threading.Tasks;

namespace Quartz.NET.Extend
{

    // https://github.com/midianok/nuget-quartz-lambda
    // Plus My custom modifications
    internal static class QuartzExtentions
    {
        internal const string JobDataActionKeyName = "job_action_param";
        internal const string JobDataInstanceKeyName = "job_key_param";
        internal const string JobDataInstanceBodyValueKeyName = "job_body_serialized_param";
        internal const string JobDataInstanceBodyTypeKeyName = "job_body_type_param";
        internal const string JobDataInstanceConfigurationName = "job_configuration_param";
        internal static Task<DateTimeOffset> ScheduleJob(this IScheduler scheduler, string actionIdentifier, string instanceKey, object actionBodyArgument, JobConfigurationModel conf)
        {
            if (actionBodyArgument == null)
            {
                actionBodyArgument = new object();
            }
            var data = new JobDataMap
                                {
                                    { JobDataActionKeyName, actionIdentifier },
                                    { JobDataInstanceKeyName, instanceKey },
                                    { JobDataInstanceBodyValueKeyName, JsonConvert.SerializeObject( actionBodyArgument) },
                                    { JobDataInstanceBodyTypeKeyName, actionBodyArgument.GetType().AssemblyQualifiedName },
                                    { JobDataInstanceConfigurationName,JsonConvert.SerializeObject( conf )}
                                };

            var jobDetail = JobBuilder
                .Create<DynamicJob>()
                .WithIdentity(new JobKey(GetJobUniqueKey(actionIdentifier, instanceKey)))
                .UsingJobData(data)
                .Build();

            var builder = TriggerBuilder.Create()
                .StartAt(DateTimeOffset.UtcNow.Add(conf.Delay));

            if (conf.IsCron)
            {
                builder = builder.WithCronSchedule(conf.CronExpression);
            }
            else
            {

                builder = builder.WithSimpleSchedule(s =>
                {
                    _ = conf.RepeatCount.HasValue ?
                                s.WithInterval(conf.RecurringInterval.Value).WithRepeatCount(conf.RepeatCount.Value).WithMisfireHandlingInstructionIgnoreMisfires()
                              : s.WithInterval(conf.RecurringInterval.Value).RepeatForever().WithMisfireHandlingInstructionIgnoreMisfires();
                });
            }

            var trigger = builder.Build();
            return scheduler.ScheduleJob(jobDetail, trigger);
        }

        internal static string GetJobUniqueKey(string identifier, string instanceKey) => $"{identifier}__{instanceKey}";

    }
}
