﻿using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Admin.Repositories;
using DigitalWorldOnline.Application.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Repositories.Admin;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
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
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.PacketProcessors;

namespace DigitalWorldOnline.Game
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
                Console.WriteLine("Terminating by unhandled exception...");
            }
            else
                Console.WriteLine("Received unhandled exception.");

            Console.ReadLine();
        }

        public static IHost CreateHostBuilder(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var host = Host.CreateDefaultBuilder(args)
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
                    services.AddSingleton<AssetsLoader>();
                    services.AddSingleton<ConfigsLoader>();
                    services.AddSingleton<DropManager>();
                    services.AddSingleton<StatusManager>();
                    services.AddSingleton<ExpManager>();
                    services.AddSingleton<PartyManager>();
                    services.AddSingleton<EventManager>();

                    services.AddSingleton<EventQueueManager>();

                    services.AddSingleton<MapServer>();
                    services.AddSingleton<PvpServer>();
                    services.AddSingleton<EventServer>();
                    services.AddSingleton<DungeonsServer>();
                    services.AddSingleton<GameMasterCommandsProcessor>();
                    services.AddSingleton<PlayerCommandsProcessor>();
                    services.AddSingleton<BanForCheating>();

                    services.AddSingleton<ISender, ScopedSender<Mediator>>();
                    services.AddSingleton<IProcessor, GamePacketProcessor>();
                    services.AddSingleton(ConfigureLogger(context.Configuration));
                    services.AddHostedService<GameServer>();

                    services.AddMediatR(cfg => {
                        cfg.RegisterServicesFromAssembly(typeof(DigitalWorldOnline.Application.Separar.Queries.EventsConfigQueryHandler).Assembly);
                    });
                    services.AddTransient<Mediator>();

                    AddAutoMapper(services);
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
            SingletonResolver.Services = host.Services;
            return host;
        }

        private static void AddAutoMapper(IServiceCollection services)
        {
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
        }

        private static void AddProcessors(IServiceCollection services)
        {
            var packetProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IGamePacketProcessor).IsAssignableFrom(t) && !t.IsInterface)
                .ToList();

            packetProcessors.ForEach(processor => { services.AddSingleton(typeof(IGamePacketProcessor), processor); });
        }
        private static ILogger ConfigureLogger(IConfiguration configuration)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File(configuration["Log:VerboseRepository"] ?? "logs\\Verbose\\GameServer",
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Verbose,
                    retainedFileCountLimit: 10)
                .WriteTo.File(configuration["Log:DebugRepository"] ?? "logs\\Debug\\GameServer",
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    retainedFileCountLimit: 5)
                .WriteTo.File(configuration["Log:InformationRepository"] ?? "logs\\Information\\GameServer",
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    retainedFileCountLimit: 5)
                .WriteTo.File(configuration["Log:WarningRepository"] ?? "logs\\Warning\\GameServer",
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    retainedFileCountLimit: 5)
                .WriteTo.File(configuration["Log:ErrorRepository"] ?? "logs\\Error\\GameServer",
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    retainedFileCountLimit: 5)
                .CreateLogger();
        }
    }
}