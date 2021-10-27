﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using Host.Modules;
using Infrastructure.EfDataAccess;
using Infrastructure.Http;
using InfrastructureBase.AopFilter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oxygen.IocModule;
using Oxygen.Mesh.Dapr;
using Oxygen.ProxyGenerator.Implements;
using Oxygen.Server.Kestrel.Implements;
using System.IO;
using System.Threading.Tasks;

namespace Host
{
    class Program
    {
        private static IConfiguration Configuration { get; set; }
        static async Task Main(string[] args)
        {
            await CreateDefaultHost(args).Build().RunAsync();
        }
        static IHostBuilder CreateDefaultHost(string[] _) => new HostBuilder()
                .ConfigureWebHostDefaults(webhostbuilder => {
                    //注册成为oxygen服务节点
                    webhostbuilder.StartOxygenServer<OxygenActorStartup>((config) => {
                        config.Port = 80;
                        config.PubSubCompentName = "pubsub";
                        config.StateStoreCompentName = "statestore";
                        config.TracingHeaders = "Authentication,AuthIgnore";
                        config.UseCors = true;
                    });
                })
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json");
                    Configuration = config.Build();
                })
                .ConfigureContainer<ContainerBuilder>(builder =>
                {
                    //注入oxygen依赖
                    builder.RegisterOxygenModule();
                    //注入业务依赖
                    builder.RegisterModule(new ServiceModule());
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient();
                    //注册自定义HostService
                    services.AddHostedService<CustomerService>();
                    //注册全局拦截器
                    LocalMethodAopProvider.RegisterPipelineHandler(AopHandlerProvider.ContextHandler, AopHandlerProvider.BeforeSendHandler, AopHandlerProvider.AfterMethodInvkeHandler, AopHandlerProvider.ExceptionHandler);
                    //注册鉴权拦截器
                    AccountAuthenticationHandler.RegisterAllFilter();
                    //注册自定义Attribute AOP拦截器(需要注册全局拦截器才有效)
                    AopFilterManager.RegisterAllFilter();
                    services.AddLogging(configure =>
                    {
                        configure.AddConfiguration(Configuration.GetSection("Logging"));
                        configure.AddConsole();
                    });
                    services.AddDbContext<EfDbContext>(options => options.UseNpgsql(Configuration.GetSection("SqlConnectionString").Value));
                    services.AddAutofac();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory());
    }
}
