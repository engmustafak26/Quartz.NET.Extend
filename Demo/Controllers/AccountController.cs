using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.NET.Extend;
using Demo.Services;

namespace Demo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {


        private readonly ILogger<AccountController> _logger;
        public AccountController(ILogger<AccountController> logger)
        {
            _logger = logger;
        }



        [Route("RegisterNewUser")]
        [HttpPost]
        public async Task<IActionResult> RegisterNewUser()
        {
            // register new use
            // ...

            // send reminder email every (1) day to new user for account activation
            string databaseRecordKey = 1001.ToString();

            await QuartzWrapper.addToJobs(async (serviceProvider, argument) =>
                                                {
                                                    var sendEmail = serviceProvider.GetRequiredService<ISendEmail>();

                                                    var typedArgument = argument as SimpleDataStructure;
                                                    Console.WriteLine("Value passed To Job Code => " + typedArgument.Value);

                                                    await sendEmail.SendEmailAsync($"{typedArgument.Value} => " + DateTime.Now);

                                                },
                            identifier: "send reminder mail to new user for account activation",
                                instanceKey: databaseRecordKey,
                                codeCustomArgument: new SimpleDataStructure { Value = "Welcome Mustafa, please Activate your mail" },
                                    interval: TimeSpan.FromSeconds(05),
                                    repeatCount: 5);


            return Ok(new { Message = "user registered successfully, check your mail" });
        }

        [Route("ControlMailActivationReminderRate")]
        [HttpGet]
        public async Task<IActionResult> ControlMailActivationReminderRate([FromQuery] string message, [FromQuery] int seconds)
        {
            string databaseRecordKey = 1001.ToString();

            // edit the inline job scheduling regard to business change
            await QuartzWrapper.EditJob(identifier: "send reminder mail to new user for account activation",
                                     instanceKey: databaseRecordKey,
                                        codeCustomArgument: new SimpleDataStructure { Value = message },
                                          interval: TimeSpan.FromSeconds(seconds));


            return Ok();
        }


        [Route("UserActivateEmail")]
        [HttpGet]
        public async Task<IActionResult> UserActivateEmail()
        {
            // ...

            string databaseRecordKey = 1001.ToString();

            // edit the inline job scheduling regard to business change
            await QuartzWrapper.DeleteJob(identifier: "send reminder mail to new user for account activation", instanceKey: databaseRecordKey);


            return Ok();
        }
    }







    public class SimpleDataStructure
    {
        public string Value { get; set; }
    }

}
