using DigitalWorldOnline.Application.Admin.Repositories;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Repositories.Admin;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Infrastructure;
using DigitalWorldOnline.Infrastructure.Mapping;
using DigitalWorldOnline.Infrastructure.Repositories.Account;
using DigitalWorldOnline.Infrastructure.Repositories.Admin;
using DigitalWorldOnline.Infrastructure.Repositories.Character;
using DigitalWorldOnline.Infrastructure.Repositories.Config;
using DigitalWorldOnline.Infrastructure.Repositories.Routine;
using DigitalWorldOnline.Infrastructure.Repositories.Server;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Globalization;
using System.Reflection;

namespace DigitalWorldOnline.Character
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Run();
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(((Exception)e.ExceptionObject).InnerException);
            if (e.IsTerminating)
            {
                var message = "";
                var exceptionStackTrace = "";
                if (e.ExceptionObject is Exception exception) 
                {
                    message =  exception.Message;
                    exceptionStackTrace = exception.StackTrace;
                }
                Console.WriteLine($"{message}");
                Console.WriteLine($"{exceptionStackTrace}");
                Console.WriteLine("Terminando por exceção não tratada ...");
            }
            else
                Console.WriteLine("Recebeu exceção não atendida.");

            Console.ReadLine();
        }

        public static IHost CreateHostBuilder(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            return Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .UseEnvironment("Development")
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContext<DatabaseContext>();

                    services.AddScoped<IAdminQueriesRepository, AdminQueriesRepository>();
                    services.AddScoped<IAdminCommandsRepository, AdminCommandsRepository>();

                    services.AddScoped<IAccountQueriesRepository, AccountQueriesRepository>();
                    services.AddScoped<IAccountCommandsRepository, AccountCommandsRepository>();

                    services.AddScoped<IServerQueriesRepository, ServerQueriesRepository>();
                    services.AddScoped<IServerCommandsRepository, ServerCommandsRepository>();

                    services.AddScoped<ICharacterQueriesRepository, CharacterQueriesRepository>();
                    services.AddScoped<ICharacterCommandsRepository, CharacterCommandsRepository>();

                    services.AddScoped<IConfigQueriesRepository, ConfigQueriesRepository>();
                    services.AddScoped<IConfigCommandsRepository, ConfigCommandsRepository>();

                    services.AddScoped<IRoutineRepository, RoutineRepository>();

                    //services.AddScoped<IEmailService, EmailService>();
                    services.AddSingleton<ISender, ScopedSender<Mediator>>();
                    services.AddSingleton<IProcessor, CharacterPacketProcessor>();
                    services.AddSingleton(ConfigureLogger(context.Configuration));

                    services.AddHostedService<CharacterServer>();
                    services.AddTransient<Mediator>();

                    // Registrar os handlers do MediatR
                    services.AddMediatR(cfg => {
                        cfg.RegisterServicesFromAssembly(typeof(DigitalWorldOnline.Application.Separar.Queries.CharacterByIdQueryHandler).Assembly);
                    });

                    services.AddAutoMapper(cfg =>
                    {
                        cfg.AddProfile<AccountProfile>();
                        cfg.AddProfile<AssetsProfile>();
                        cfg.AddProfile<CharacterProfile>();
                        cfg.AddProfile<ConfigProfile>();
                        cfg.AddProfile<DigimonProfile>();
                        cfg.AddProfile<GameProfile>();
                        cfg.AddProfile<SecurityProfile>();
                        cfg.AddProfile<ArenaProfile>();
                    });

                    // Registrar os processadores de pacotes de autenticação
                    AddProcessors(services);
                })
                .ConfigureHostConfiguration(hostConfig =>
                {
                    hostConfig.SetBasePath(Directory.GetCurrentDirectory())
                              .AddEnvironmentVariables(Constants.Configuration.EnvironmentPrefix)
                              .AddUserSecrets<Program>();
                    hostConfig.AddEnvironmentVariables("DMO_");
                })
                .Build();
        }

        private static void AddProcessors(IServiceCollection services)
        {
            var packetProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(ICharacterPacketProcessor).IsAssignableFrom(t) && !t.IsInterface)
                .ToList();

            packetProcessors.ForEach(processor => { services.AddSingleton(typeof(ICharacterPacketProcessor), processor); });
        }


        private static ILogger ConfigureLogger(IConfiguration configuration)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File(
                    path: configuration["Log:VerboseRepository"] ?? "logs\\Verbose\\CharacterServer",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    restrictedToMinimumLevel: LogEventLevel.Verbose)
                .WriteTo.File(
                    path: configuration["Log:DebugRepository"] ?? "logs\\Debug\\CharacterServer",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.File(
                    path: configuration["Log:InformationRepository"] ?? "logs\\Information\\CharacterServer",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File(
                    path: configuration["Log:WarningRepository"] ?? "logs\\Warning\\CharacterServer",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    restrictedToMinimumLevel: LogEventLevel.Warning)
                .WriteTo.File(
                    path: configuration["Log:ErrorRepository"] ?? "logs\\Error\\CharacterServer",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    restrictedToMinimumLevel: LogEventLevel.Error)
                .CreateLogger();
        }
    }
}