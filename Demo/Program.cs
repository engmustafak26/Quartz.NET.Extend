using Quartz;
using Quartz.NET.Extend;
using Demo.Services;

namespace Demo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            QuartzWrapper.RegisterCodeAsync().GetAwaiter().GetResult();
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            
            builder.Services.AddTransient<ISendEmail, SendEmail>();

            // Configuration For Original Quartz nuget
            builder.Services.Configure<QuartzOptions>(builder.Configuration.GetSection("Quartz"));
            builder.Services.AddQuartz(q =>
            {
                q.UsePersistentStore(s =>
                {
                    s.UseProperties = true;
                    s.UseNewtonsoftJsonSerializer();
                });

            });
            builder.Services.AddQuartzHostedService(opt =>
              {
                  opt.WaitForJobsToComplete = true;
              });



            var app = builder.Build();
            app.Services.AddQuartzServices(rescheduleAlreadyActiveJobs: false);
            QuartzWrapper.RemoveAllJobsAsync().GetAwaiter().GetResult();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }


    }



}
