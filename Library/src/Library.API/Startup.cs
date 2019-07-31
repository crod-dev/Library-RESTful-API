using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Library.API.Services;
using Library.API.Entities;
using Microsoft.EntityFrameworkCore;
using Library.API.Helpers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json.Serialization;
using AspNetCoreRateLimit;

namespace Library.API
{
    public class Startup
    {
        public static IConfiguration Configuration;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(setupAction =>
            {
                setupAction.ReturnHttpNotAcceptable = true;
                setupAction.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
                setupAction.InputFormatters.Add(new XmlDataContractSerializerInputFormatter(setupAction));
                // 10 comment out
                //setupAction.InputFormatters.Add(new XmlSerializerInputFormatter(setupAction));
                // 10 create new formatters
                var xmlDataContractSerializerInputFormatter =
                new XmlSerializerInputFormatter(setupAction);
                xmlDataContractSerializerInputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.authorwithdateofdeath.full+xml");
                setupAction.InputFormatters.Add(xmlDataContractSerializerInputFormatter);

                // 10
                var jsonInputFormatter = setupAction.InputFormatters
                .OfType<JsonInputFormatter>().FirstOrDefault();

                if (jsonInputFormatter != null)
                {
                    jsonInputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.author.full+json");
                    jsonInputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.authorwithdateofdeath.full+json");
                }

                // 10 Add media type formatter to support vendor specific media type
                var jsonOutputFormatter = setupAction.OutputFormatters.OfType<JsonOutputFormatter>().FirstOrDefault();

                if (jsonOutputFormatter != null)
                {
                    jsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.marvin.hateoas+json");
                }
            })
            .AddJsonOptions(options =>
            {
                options.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver();
            });
            // register the DbContext on the container, getting the connection string from
            // appSettings (note: use this during development; in a production environment,
            // it's better to store the connection string in an environment variable)
            var connectionString = Configuration["connectionStrings:libraryDBConnectionString"];
            services.AddDbContext<LibraryContext>(o => o.UseSqlServer(connectionString));

            // register the repository
            services.AddScoped<ILibraryRepository, LibraryRepository>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddScoped<IUrlHelper, UrlHelper>(implementationFactory =>
            {
                var actionContext =
                implementationFactory.GetService<IActionContextAccessor>().ActionContext;
                return new UrlHelper(actionContext);
            });
            services.AddTransient<IPropertyMappingService, PropertyMappingService>();
            services.AddTransient<ITypeHelperService, TypeHelperService>();

            // 11 adding cache header service after installing marvin.cache.headers nuGet package
            services.AddHttpCacheHeaders(
                (expirationModelOptions)
                =>
                {
                    expirationModelOptions.MaxAge = 600;
                },
                (validationModelOptions)
                =>
                {
                    validationModelOptions.AddMustRevalidate = true;
                });

            // 11 adding cache store service
            services.AddResponseCaching();

            // 12 register the services memory cache so the middleware nuget throttling middleware can use it
            services.AddMemoryCache();
            // 12 ip rate limiting middleware settings: call configure and pass in the options that we want to configure (IpRateLimitOptions)
            services.Configure<IpRateLimitOptions>((options) =>
            {
                // 12 options parameter, many propterties to configure the ip rate limit middleware
                options.GeneralRules = new System.Collections.Generic.List<RateLimitRule>()
                {                    
                    new RateLimitRule()
                    {
                        // 12 limit requests to the full api
                        Endpoint = "*",
                        // 12 limit any resource to 3 requests per 5 minutes
                        Limit = 1000,
                        Period = "5m"
                    },
                    new RateLimitRule()
                    {
                        // 12 combine different rules
                        Endpoint = "*",
                        // 12 limit any resource to 2 requests per 10 seconds
                        Limit = 2,
                        Period = "10s"
                    }
                        // 12 *As mentioned, there are a lot of options to configure. 
                        // We can add policies for each IP. We can configure IP ranges and client IDs, 
                        // limit requests, depending on the methods or on the resource. So, 10 posts per 
                        // minute to the author's resource is allowed, but we only get 100 each hour. 
                        // And we can even read those options from configuration files instead of inputting them in code.
                };
            });
            // 12 to be created once and not for each request, this is to store the policy and rate counter 
            // across all requests to the api, therefore singleton
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory, LibraryContext libraryContext)
        {
            loggerFactory.AddConsole();
            loggerFactory.AddDebug(LogLevel.Information);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                        if (exceptionHandlerFeature != null)
                        {
                            var logger = loggerFactory.CreateLogger("Global exception logger");
                            logger.LogError(500, exceptionHandlerFeature.Error,
                                exceptionHandlerFeature.Error.Message);
                        }
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("An unexpected fault happened. Try again later.");
                    });
                });
            }

            AutoMapper.Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<Entities.Author, Models.AuthorDto>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src =>
                $"{src.FirstName} {src.LastName}"))
                .ForMember(dest => dest.Age, opt => opt.MapFrom(src =>
                src.DateOfBirth.GetCurrentAge(src.DateOfDeath)));

                cfg.CreateMap<Entities.Book, Models.BookDto>();
                cfg.CreateMap<Models.AuthorCreationDto, Entities.Author>();
                // 10 add mapping for author with death date
                cfg.CreateMap<Models.AuthorForCreationWithDateOfDeathDto, Entities.Author>();
                cfg.CreateMap<Models.BookCreationDto, Entities.Book>();
                cfg.CreateMap<Models.BookUpdateDto, Entities.Book>();
                cfg.CreateMap<Entities.Book, Models.BookUpdateDto>();
            });

            libraryContext.EnsureSeedDataForContext();

            // 12 rate limiting middleware should be registered in the pipeline before any other
            // because this rejects the requests if limits are hit 
            app.UseIpRateLimiting();

            // 11 Add cache store to the pipeline befroe the cache headers
            app.UseResponseCaching();

            // 11 add middleware (cache headers) to the request pipeline (order is important, should be before mvc)
            app.UseHttpCacheHeaders();

            app.UseMvc();
        }
    }
}
