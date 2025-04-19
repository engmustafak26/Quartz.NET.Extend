

# Quartz.NET.Extend

[![NuGet Version](https://img.shields.io/nuget/v/Quartz.NET.Extend.svg?style=flat-square)](https://www.nuget.org/packages/Quartz.NET.Extend/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)

**Developed by Mustafa Kamal**

A revolutionary Quartz.NET extension that enables inline job scheduling with full runtime management capabilities.

## 🚀 Technical Overview

`Quartz.NET.Extend` transforms job scheduling by:

- **Eliminating boilerplate**: No need for separate `IJob` implementations
- **Contextual scheduling**: Define jobs right where they're needed in business logic
- **Runtime control**: Edit or delete schedules without application restart
- **Type-safe arguments**: Pass complex objects directly to jobs
- **DI integration**: Automatic service resolution via `IServiceProvider`

## 🆚 Traditional vs Extended Approach

### Traditional IJob Implementation

```csharp
// Separate class required
public class ActivationReminderJob : IJob 
{
    private readonly ISendEmail _emailService;
    
    public ActivationReminderJob(ISendEmail emailService) 
    {
        _emailService = emailService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = (SimpleDataStructure)context.MergedJobDataMap["args"];
        await _emailService.SendEmailAsync($"{data.Value} => {DateTime.Now}");
    }
}

// Scheduling code elsewhere
var job = JobBuilder.Create<ActivationReminderJob>()
    .UsingJobData("args", new SimpleDataStructure { Value = "Welcome message" })
    .Build();

await scheduler.ScheduleJob(job, 
    TriggerBuilder.Create()
        .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(7, 53))
        .Build());
```

### With Quartz.NET.Extend

```csharp
[Route("RegisterNewUser")]
[HttpPost]
public async Task<IActionResult> RegisterNewUser()
{
    string databaseRecordKey = 1001.ToString();

    await QuartzWrapper.addToJobs(async (sp, arg) =>
    {
        var emailService = sp.GetRequiredService<ISendEmail>();
        var data = arg as SimpleDataStructure;
        await emailService.SendEmailAsync($"{data.Value} => {DateTime.Now}");
    },
    identifier: "send reminder mail to new user for account activation",
    instanceKey: databaseRecordKey,
    codeCustomArgument: new SimpleDataStructure { 
        Value = "Welcome Mustafa, please Activate your mail" 
    },
    CronScheduleBuilder.DailyAtHourAndMinute(7, 53));

    return Ok(new { Message = "User registered successfully" });
}
```
![Quartz.NET.Extend Workflow Diagram](https://raw.githubusercontent.com/engmustafak26/Quartz.NET.Extend/refs/heads/master/HLD.png)  
*Figure 1: Inline Job Scheduling Architecture*



## 🧠 Technical Deep Dive

`Quartz.NET.Extend` transforms job scheduling by:
- **Runtime Code Generation**: Uses Roslyn to compile lambdas into `IJob` implementations
- **Contextual Binding**: Ties jobs to business entities via `instanceKey`
- **Type-Safe Marshaling**: Serializes arguments using `System.Text.Json`
- **Dynamic Schedule Management**: Edits triggers without app restart
- **DI-Aware Execution**: Resolves services via `IServiceProvider`



## 📦 Installation

### Prerequisite Packages
```powershell
Install-Package Quartz
Install-Package Quartz.Extensions.DependencyInjection
Install-Package Quartz.Extensions.Hosting
Install-Package Quartz.Serialization.Json
Install-Package Microsoft.CodeAnalysis.Common
Install-Package Microsoft.CodeAnalysis.CSharp
```

### Program.cs Configuration
```csharp
using Quartz.NET.Extend;

// ... existing builder configuration ...

if (builder.Environment.IsDevelopment())
{
    await QuartzWrapper.RegisterCodeAsync();
}

var app = builder.Build();

app.Services.AddQuartzServices(rescheduleAlreadyActiveJobs: false);

if (app.Environment.IsDevelopment())
{
    await QuartzWrapper.RemoveAllJobsAsync();
}
else 
{
    QuartzWrapper.RescheduleAlreadyActiveJobsAsync().GetAwaiter().GetResult();
}
```


## 🛠 API Reference

### Job Management
```csharp
// Create job with cron expression
public static async Task addToJobs(
    Func<IServiceProvider, object, Task> jobDelegate,
    string identifier,
    string instanceKey,
    object codeCustomArgument,
    string cronExpression,
    TimeSpan delay = default)

// Edit existing job
public static async Task EditJob(
    string identifier,
    string instanceKey,
    object newArgument,
    TimeSpan newInterval)

// Delete job
public static async Task DeleteJob(
    string identifier, 
    string instanceKey)
```

## 🌍 Real-World Examples

### E-Commerce Order Processing
```csharp
[HttpPost]
public async Task PlaceOrder(Order order)
{
    await _orderService.Create(order);

    // Abandoned cart reminder
    await QuartzWrapper.addToJobs(async (sp, arg) => 
    {
        var order = arg as Order;
        var cartService = sp.GetRequiredService<ICartService>();
        if (!await cartService.IsCompleted(order.Id))
        {
            await sp.GetRequiredService<INotificationService>()
                   .SendReminder(order.UserId);
        }
    },
    identifier: "abandoned-cart-reminder",
    instanceKey: order.Id.ToString(),
    codeCustomArgument: order,
    CronScheduleBuilder.DailyAtHourAndMinute(20, 0));
}
```

### IoT Device Monitoring
```csharp
public async Task RegisterDevice(Device device)
{
    // Health check every 5 minutes
    await QuartzWrapper.addToJobs(async (sp, arg) =>
    {
        var device = arg as Device;
        var healthChecker = sp.GetRequiredService<IDeviceMonitor>();
        await healthChecker.CheckStatus(device.Id);
    },
    identifier: "device-health-check",
    instanceKey: device.MacAddress,
    codeCustomArgument: device,
    interval: TimeSpan.FromMinutes(5));
}
```

### Newsletter System
```csharp
public async Task ScheduleNewsletter(Newsletter newsletter, DateTime sendTime)
{
    await QuartzWrapper.addToJobs(async (sp, arg) =>
    {
        var newsletter = arg as Newsletter;
        await sp.GetRequiredService<IMailingService>()
               .SendBulk(newsletter);
    },
    identifier: "newsletter-delivery",
    instanceKey: newsletter.Id.ToString(),
    codeCustomArgument: newsletter,
    CronScheduleBuilder.DailyAtHourAndMinute(sendTime.Hour, sendTime.Minute));
}
```

## 📜 License
MIT © 2025 Mustafa Kamal


### Key Features Highlighted:
- Clear comparison between traditional and new approaches
- Complete installation instructions
- Ready-to-use configuration snippet
- API reference table
- Badges for version and license
- Clean markdown formatting for GitHub

Would you like me to add any additional sections such as:
- Advanced configuration options
- Troubleshooting guide
- Performance benchmarks
- Contribution guidelines?