using MedRecPro.Data;
using MedRecPro.Api.Controllers;
using MedRecPro.Exceptions;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RazorLight;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Provides composition-root extension methods for API controller, serialization, view, and RazorLight registration.
    /// </summary>
    /// <remarks>
    /// Controller and view registrations remain distinct because the API JSON options and MVC view filters serve different runtime surfaces.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMedRecProApiControllers(configuration);
    /// services.AddMedRecProViews(configuration);
    /// </code>
    /// </example>
    /// <seealso cref="IMvcBuilder"/>
    /// <seealso cref="IRazorLightEngine"/>
    public static class MedRecProMvcExtensions
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Registers JSON serialization settings and API controller services.
        /// </summary>
        /// <remarks>
        /// The ignore-empty-collections setting, global authorization exception filter, and shared problem-details policy
        /// preserve API serialization while normalizing validation and unexpected-error responses.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProApiControllers(configuration);
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Configuration containing serialization feature flags.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="AuthorizationExceptionFilter"/>
        /// <seealso cref="ConfigurableIgnoreEmptyContractHelper"/>
        public static IServiceCollection AddMedRecProApiControllers(this IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            bool ignoreEmptyCollections;

            Boolean.TryParse(configuration["IgnoreEmptyObjectsWhenSerializing"], out ignoreEmptyCollections);

            services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Extensions["traceId"] =
                        Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                };
            });

            services.AddExceptionHandler<MedRecProExceptionHandler>();

            #region Ignore Empty Fields When Serializing
            // Configure JSON options
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.SerializerOptions.WriteIndented = true; // Optional: for readable JSON
            });

            // For controllers/API endpoints, also configure:
            services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                options.SerializerOptions.WriteIndented = true;
            });

            #endregion

            #region Newtonsoft Options
            services.AddControllers(options =>
            {
                // Split Label feature controllers keep the original public route name through this convention.
                options.Conventions.Add(new LabelFeatureControllerModelConvention());

                // Register the authorization exception filter globally
                // This catches AuthorizationException, UserRoleAuthorizationException,
                // and ActorAuthorizationException thrown by the authorization filters
                options.Filters.Add<AuthorizationExceptionFilter>();
            })
            .AddNewtonsoftJson(options =>
            {
                // Ignore null values
                options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;

                // Ignore default values (empty collections, default primitives)
                options.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;

                // Pretty print JSON
                options.SerializerSettings.Formatting = Formatting.Indented;

                // Custom configuration - ignore empty collections and objects
                if (ignoreEmptyCollections)
                    options.SerializerSettings.ContractResolver = new ConfigurableIgnoreEmptyContractHelper(
                        ignoreEmptyCollections: true,
                        ignoreEmptyObjects: true);
                else
                    options.SerializerSettings.ContractResolver = new ConfigurableIgnoreEmptyContractHelper(
                        ignoreEmptyCollections: false,
                        ignoreEmptyObjects: false);
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
            #endregion

            /**************************************************************/
            // Explicitly retain the standard HTTP 400 outcome while making MVC validation responses
            // use the same RFC 7807 media type and correlation data as unexpected request failures.
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    var problemDetails = new ValidationProblemDetails(actionContext.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "One or more validation errors occurred.",
                        Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                        Instance = actionContext.HttpContext.Request.Path.Value
                    };

                    problemDetails.Extensions["traceId"] =
                        Activity.Current?.Id ?? actionContext.HttpContext.TraceIdentifier;

                    var result = new BadRequestObjectResult(problemDetails);
                    result.ContentTypes.Add("application/problem+json");
                    return result;
                };
            });

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers MVC view services, RazorLight template rendering, and the view rendering service.
        /// </summary>
        /// <remarks>
        /// The background-processing feature flag continues to decide whether the activity-log action filter is attached to controller views.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProViews(configuration);
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Configuration containing feature flags.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="ActivityLogActionFilter"/>
        /// <seealso cref="ViewRenderService"/>
        /// <seealso cref="IRazorLightEngine"/>
        public static IServiceCollection AddMedRecProViews(this IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            #region View Configuration

            // Enable ASP.NET Core Razor Views with Activity Logging Filter
            if (configuration.GetValue<bool>("FeatureFlags:BackgroundProcessingEnabled", true))
                services.AddControllersWithViews(options =>
                {
                    options.Filters.Add<ActivityLogActionFilter>();
                });
            else
                services.AddControllersWithViews();

            // RazorLight for programmatic templates (after your existing custom services)
            services.AddSingleton<IRazorLightEngine>(serviceProvider =>
            {
                var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

                return new RazorLightEngineBuilder()
                    .UseFileSystemProject(Path.Combine(environment.ContentRootPath, "Views"))
                    .UseEmbeddedResourcesProject(typeof(global::Program))
                    .UseMemoryCachingProvider()
                    .AddMetadataReferences(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(global::Program).Assembly.Location))
                    .AddMetadataReferences(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(User).Assembly.Location))
                    .AddMetadataReferences(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(ApplicationDbContext).Assembly.Location))
                    .EnableDebugMode(environment.IsDevelopment())
                    .Build();
            });

            // View rendering service for ASP.NET Core views
            services.AddScoped<IViewRenderService, ViewRenderService>();
            #endregion

            return services;

            #endregion
        }

        #endregion
    }
}
