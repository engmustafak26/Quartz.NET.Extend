using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Quartz.Impl.Matchers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quartz.NET.Extend
{
    public static class QuartzWrapper
    {
        internal static IServiceProvider? ServiceProvider { get; set; }


        public static void AddQuartzServices(this IServiceProvider provider, bool rescheduleAlreadyActiveJobs = true)
        {
            ServiceProvider = provider;
            if (rescheduleAlreadyActiveJobs)
            {
                RescheduleAlreadyActiveJobsAsync().GetAwaiter().GetResult();
            }
        }

        public static async Task RescheduleAlreadyActiveJobsAsync()
        {
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            foreach (var groupName in await scheduler.GetJobGroupNames())
            {
                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(groupName));
                foreach (var jobKey in jobKeys)
                {
                    var detail = await scheduler.GetJobDetail(jobKey);
                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    bool canReschedule = false;
                    foreach (var trigger in triggers)
                    {
                        if (trigger is ICronTrigger)
                        {
                            canReschedule = true;
                        }

                        else if (trigger is ISimpleTrigger simpleTrigger && trigger.GetNextFireTimeUtc().HasValue)
                        {
                            canReschedule = true;

                        }

                        await scheduler.DeleteJob(jobKey);

                        if (canReschedule)
                        {
                            var identifier = detail?.JobDataMap[QuartzExtentions.JobDataActionKeyName]?.ToString();
                            var recurringConfiguration = detail?.JobDataMap[QuartzExtentions.JobDataInstanceConfigurationName]?.ToString();
                            bool isStillExist = await RoslynManager.ReadJobsCodeFromFileAsync(identifier) != null;

                            if (!string.IsNullOrWhiteSpace(recurringConfiguration) && isStillExist)
                            {
                                var instanceKey = detail?.JobDataMap[QuartzExtentions.JobDataInstanceKeyName]?.ToString();
                                var jobRecurringConf = JsonConvert.DeserializeObject<JobConfigurationModel>(recurringConfiguration);

                                var stringArgument = detail?.JobDataMap[QuartzExtentions.JobDataInstanceBodyValueKeyName]?.ToString();
                                var argumentTypeString = detail?.JobDataMap[QuartzExtentions.JobDataInstanceBodyTypeKeyName].ToString();
                                var argumentType = TypeLoader.LoadType(argumentTypeString);
                                var typedArgument = JsonConvert.DeserializeObject(stringArgument, argumentType);

                                await addToJobImplicit(identifier, instanceKey, typedArgument, jobRecurringConf);

                            }
                        }

                    }
                }
            }
        }

        public static async Task RemoveAllJobsAsync()
        {
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            var jobGroups = await scheduler.GetJobGroupNames();
            foreach (var group in jobGroups)
            {
                var groupMatcher = GroupMatcher<JobKey>.GroupEquals(group);
                var jobKeys = await scheduler.GetJobKeys(groupMatcher);

                await scheduler.DeleteJobs(jobKeys.ToList());
            }
            await scheduler.Clear();
        }
        public static async Task RegisterCodeAsync()
        {

            string? solutionPath = GetSolutionDirectory();

            var files = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);

            Dictionary<string, string[]> sourceCodes = new Dictionary<string, string[]>();
            foreach (var file in files)
            {
                var result = await RoslynManager.GetCode(file);
                if (result.Any())
                {
                    sourceCodes = sourceCodes.Concat(result).ToDictionary(pair => pair.Key, pair => pair.Value);

                }

            }

            await RoslynManager.WriteCodeToFile(sourceCodes);

        }



        public static async Task addToJobs(Func<IServiceProvider, object, Task> func, string identifier, string instanceKey, object codeCustomArgument, TimeSpan interval, int? repeatCount = null, TimeSpan delay = default)
        {
            JobConfigurationModel conf = new JobConfigurationModel
            {
                RecurringInterval = interval,
                Delay = delay,
                RepeatCount = repeatCount,
            };
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            var result = await scheduler.ScheduleJob(identifier, instanceKey, codeCustomArgument, conf);
        }
        public static async Task addToJobs(Func<IServiceProvider, object, Task> func, string identifier, string instanceKey, object codeCustomArgument, CronScheduleBuilder cronBuilder, TimeSpan delay = default)
        {
            // Build a temporary trigger to extract the expression
            ICronTrigger tempTrigger = (ICronTrigger)TriggerBuilder.Create()
                                                          .WithSchedule(cronBuilder)
                                                          .Build();

            JobConfigurationModel conf = new JobConfigurationModel
            {
                CronExpression = tempTrigger!.CronExpressionString!,
                Delay = delay,
            };

            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            var result = await scheduler.ScheduleJob(identifier, instanceKey, codeCustomArgument, conf);
        }
        public static async Task addToJobs(Func<IServiceProvider, object, Task> func, string identifier, string instanceKey, object codeCustomArgument, string cronExpression, TimeSpan delay = default)
        {
            JobConfigurationModel conf = new JobConfigurationModel
            {
                CronExpression = cronExpression,
                Delay = delay,
            };
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            var result = await scheduler.ScheduleJob(identifier, instanceKey, codeCustomArgument, conf);
        }

        public static async Task EditJob(string identifier, string instanceKey, object codeCustomArgument, TimeSpan interval, TimeSpan delay = default)
        {
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            var jobKey = new JobKey(QuartzExtentions.GetJobUniqueKey(identifier, instanceKey));
            var detail = await scheduler.GetJobDetail(jobKey);
            if (detail == null)
            {
                return;
            }

            await scheduler.DeleteJob(jobKey);
            DynamicJob.InvalidateJobStore(identifier);


            JobConfigurationModel conf = new JobConfigurationModel
            {
                RecurringInterval = interval,
                Delay = delay,
            };

            await addToJobImplicit(identifier, instanceKey, codeCustomArgument, conf);
        }
        public static async Task DeleteJob(string identifier, string instanceKey)
        {
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            var jobKey = new JobKey(QuartzExtentions.GetJobUniqueKey(identifier, instanceKey));
            var detail = await scheduler.GetJobDetail(jobKey);
            if (detail == null)
            {
                return;
            }

            await scheduler.DeleteJob(jobKey);
        }


        public static async Task ExecuteJobs(Func<IServiceProvider, object, Task> func, object codeCustomArgument)
        {

            await func(ServiceProvider, codeCustomArgument);
        }
        private static async Task addToJobImplicit(string identifier, string instanceKey, object codeCustomArgument, JobConfigurationModel conf)
        {
            var scheduler = await ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.ScheduleJob(identifier, instanceKey, codeCustomArgument, conf);
        }
        private static string? GetSolutionDirectory()
        {
            string currentDirectory = Directory.GetCurrentDirectory();

            while (!string.IsNullOrEmpty(currentDirectory))
            {
                if (Directory.GetFiles(currentDirectory, "*.sln").Length > 0)
                {
                    return currentDirectory; // Return directory containing .sln file
                }

                // Move to the parent directory
                currentDirectory = Directory.GetParent(currentDirectory)?.FullName ?? "";
            }

            return null; // No .sln file found
        }

    }
}
