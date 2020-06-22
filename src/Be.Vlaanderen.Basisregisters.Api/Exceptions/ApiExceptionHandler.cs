namespace Be.Vlaanderen.Basisregisters.Api.Exceptions
{
    using System.Collections.Generic;
    using System.Net.Mime;
    using System.Reflection;
    using AspNetCore.Mvc.Middleware;
    using BasicApiProblem;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class ApiExceptionHandler { }

    public static class ApiExceptionHandlerExtension
    {
        private static ILogger<ApiExceptionHandler> _logger;

        public static IApplicationBuilder UseApiExceptionHandler(
            this IApplicationBuilder app,
            ILoggerFactory loggerFactory,
            string corsPolicyName)
            => UseApiExceptionHandling(
                app,
                corsPolicyName,
                new StartupUseOptions
                {
                    Common = { LoggerFactory = loggerFactory },
                    Api = { CustomExceptionHandlers = new IExceptionHandler[] { } }
                },
                null);

        public static IApplicationBuilder UseApiExceptionHandler(
            this IApplicationBuilder app,
            ILoggerFactory loggerFactory,
            string corsPolicyName,
            IEnumerable<IExceptionHandler> customExceptionHandlers)
            => UseApiExceptionHandling(
                app,
                corsPolicyName,
                new StartupUseOptions
                {
                    Common = { LoggerFactory = loggerFactory },
                    Api = { CustomExceptionHandlers = customExceptionHandlers }
                },
                null);

        internal static IApplicationBuilder UseApiExceptionHandler(
            this IApplicationBuilder app,
            string corsPolicyName,
            StartupUseOptions startupUseOptions,
            StartupConfigureOptions? startupConfigureOptions)
            => UseApiExceptionHandling(
                app,
                corsPolicyName,
                startupUseOptions,
                startupConfigureOptions);

        private static IApplicationBuilder UseApiExceptionHandling(
            IApplicationBuilder app,
            string corsPolicyName,
            StartupUseOptions startupUseOptions,
            StartupConfigureOptions? startupConfigureOptions)
        {
            _logger = startupUseOptions.Common.LoggerFactory.CreateLogger<ApiExceptionHandler>();
            var customHandlers = startupUseOptions.Api.CustomExceptionHandlers ?? new IExceptionHandler[]{ };
            var exceptionHandler = new ExceptionHandler(_logger, customHandlers, startupConfigureOptions);

            app.UseExceptionHandler(builder =>
            {
                builder.UseCors(policyName: corsPolicyName);
                startupUseOptions.MiddlewareHooks.AfterCors?.Invoke(builder);

                builder.UseProblemDetails();
                startupUseOptions.MiddlewareHooks.AfterProblemDetails?.Invoke(builder);

                builder
                    .UseMiddleware<EnableRequestRewindMiddleware>()

                    // https://github.com/serilog/serilog-aspnetcore/issues/59
                    .UseMiddleware<AddCorrelationIdToResponseMiddleware>()
                    .UseMiddleware<AddCorrelationIdMiddleware>()
                    .UseMiddleware<AddCorrelationIdToLogContextMiddleware>()

                    .UseMiddleware<AddHttpSecurityHeadersMiddleware>(
                        startupUseOptions.Server.ServerName,
                        startupUseOptions.Server.PoweredByName,
                        startupUseOptions.Server.FrameOptionsDirective)

                    .UseMiddleware<AddRemoteIpAddressMiddleware>(startupUseOptions.Api.RemoteIpAddressClaimName)

                    .UseMiddleware<AddVersionHeaderMiddleware>(startupUseOptions.Server.VersionHeaderName);
                startupUseOptions.MiddlewareHooks.AfterMiddleware?.Invoke(builder);

                builder
                    .UseMiddleware<DefaultResponseCompressionQualityMiddleware>(new Dictionary<string, double>
                    {
                        { "br", 1.0 },
                        { "gzip", 0.9 }
                    })
                    .UseResponseCompression();
                startupUseOptions.MiddlewareHooks.AfterResponseCompression?.Invoke(builder);

                var requestLocalizationOptions = startupUseOptions
                    .Common
                    .ServiceProvider
                    .GetRequiredService<IOptions<RequestLocalizationOptions>>()
                    .Value;

                builder.UseRequestLocalization(requestLocalizationOptions);
                startupUseOptions.MiddlewareHooks.AfterRequestLocalization?.Invoke(builder);

                builder
                    .Run(async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        context.Response.ContentType = MediaTypeNames.Application.Json;

                        var error = context.Features.Get<IExceptionHandlerFeature>();
                        var exception = error?.Error;

                        // Errors happening in the Apply() stuff result in an InvocationException due to the dynamic stuff.
                        if (exception is TargetInvocationException && exception.InnerException != null)
                            exception = exception.InnerException;

                        await exceptionHandler.HandleException(exception, context);
                    });
            });

            return app;
        }
    }
}
