using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Quartz.NET.Extend
{
    internal class DynamicJob : IJob
    {
        private readonly static Dictionary<string, Type> _dynamicTypeStore = new Dictionary<string, Type>();
        private readonly static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1);
        public async Task Execute(IJobExecutionContext context)
        {

            string instanceKey = context.JobDetail.JobDataMap[QuartzExtentions.JobDataInstanceKeyName] as string;

            await _semaphoreSlim.WaitAsync(instanceKey, async () =>
            {
                string actionCode = context.JobDetail.JobDataMap[QuartzExtentions.JobDataActionKeyName] as string;

                _dynamicTypeStore.TryGetValue(actionCode, out var type);
                if (type == null)
                {
                    type = await RoslynManager.ExecuteAsync(actionCode);
                    _dynamicTypeStore.TryAdd(actionCode, type);
                }

                MethodInfo method = type.GetMethod("Exec");
                var stringArgument = context.JobDetail.JobDataMap[QuartzExtentions.JobDataInstanceBodyValueKeyName].ToString();
                var argumentTypeString = context.JobDetail.JobDataMap[QuartzExtentions.JobDataInstanceBodyTypeKeyName].ToString();
                var argumentType = TypeLoader.LoadType(argumentTypeString);
                var typedArgument = JsonConvert.DeserializeObject(stringArgument, argumentType);
                await (method.Invoke(null, new object[] { typedArgument }) as Task);
            });



        }

        public static void InvalidateJobStore(string identifier) => _dynamicTypeStore.Remove(identifier);
    }
}
