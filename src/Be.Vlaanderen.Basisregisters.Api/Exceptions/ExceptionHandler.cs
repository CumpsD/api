namespace Be.Vlaanderen.Basisregisters.Api.Exceptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AggregateSource;
    using BasicApiProblem;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using ProblemDetails = BasicApiProblem.ProblemDetails;

    public class ExceptionHandler
    {
        private readonly ILogger<ApiExceptionHandler> _logger;
        private readonly IEnumerable<ApiProblemDetailsExceptionMapper> _apiProblemDetailsExceptionMappers;
        private readonly IEnumerable<IExceptionHandler> _exceptionHandlers;

        public ExceptionHandler(
            ILogger<ApiExceptionHandler> logger,
            IEnumerable<IExceptionHandler> customExceptionHandlers)
            : this(logger, customExceptionHandlers, null) { }

        public ExceptionHandler(
            ILogger<ApiExceptionHandler> logger,
            IEnumerable<IExceptionHandler> customExceptionHandlers,
            StartupConfigureOptions? options)
            : this(logger, new List<ApiProblemDetailsExceptionMapping>(), customExceptionHandlers, options) { }

        public ExceptionHandler(
            ILogger<ApiExceptionHandler> logger,
            IEnumerable<ApiProblemDetailsExceptionMapping> apiProblemDetailsExceptionMappings,
            IEnumerable<IExceptionHandler> customExceptionHandlers,
            StartupConfigureOptions? options)
        {
            _logger = logger;
            _apiProblemDetailsExceptionMappers = apiProblemDetailsExceptionMappings
                .Select(configuration => new ApiProblemDetailsExceptionMapper(options, configuration));
            _exceptionHandlers = customExceptionHandlers
                .Concat(DefaultExceptionHandlers.GetHandlers(options));
        }

        /// <summary>Sets the exception result as HttpResponse</summary>
        public async Task HandleException(Exception exception, HttpContext context)
        {
            if (exception is ApiProblemDetailsException problemDetailsException)
            {
                var problemDetailMappings = _apiProblemDetailsExceptionMappers
                    .Where(mapping => mapping.Handles(problemDetailsException))
                    .ToList();

                if (problemDetailMappings.Count == 1)
                    throw new ProblemDetailsException(problemDetailMappings.First().Map(problemDetailsException));

                if (problemDetailMappings.Count > 1)
                    _logger.LogWarning($"Multiple mappings for {nameof(ApiProblemDetailsException)} found. Skipping specific mapping.");
            }

            var exceptionHandler = _exceptionHandlers.FirstOrDefault(handler => handler.Handles(exception));

            if (exceptionHandler == null)
                throw new ProblemDetailsException(HandleUnhandledException(exception));

            var problem = await exceptionHandler.GetApiProblemFor(exception);
            problem.ProblemInstanceUri = context.GetProblemInstanceUri();

            LogExceptionHandled(exception, problem, exceptionHandler.HandledExceptionType);
            throw new ProblemDetailsException(problem);
        }

        private void LogExceptionHandled(Exception exception, ProblemDetails problemResponse, Type handledExceptionType)
        {
            var exceptionTypeName = typeof(AggregateNotFoundException) == handledExceptionType
                ? "NotFoundException"
                : handledExceptionType.Name;

            _logger.LogInformation(
                0,
                exception,
                "[{ErrorNumber}] {HandledExceptionType} handled: {ExceptionMessage}",
                problemResponse.ProblemInstanceUri, exceptionTypeName, problemResponse.Detail);
        }

        private ProblemDetails HandleUnhandledException(Exception exception)
        {
            var problemResponse = new ProblemDetails
            {
                HttpStatus = StatusCodes.Status500InternalServerError,
                Title = ProblemDetails.DefaultTitle,
                Detail = "",
                ProblemTypeUri = ProblemDetails.GetTypeUriFor(exception as UnhandledException),
                ProblemInstanceUri = ProblemDetails.GetProblemNumber()
            };

            if (exception != null)
                _logger.LogError(0, exception, "[{ErrorNumber}] Unhandled exception!", problemResponse.ProblemInstanceUri);

            return problemResponse;
        }

        private class UnhandledException : Exception { }
    }
}
