namespace Dummy.Api.Infrastructure
{
    using Be.Vlaanderen.Basisregisters.Api;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json.Converters;
    using Responses;
    using Swashbuckle.AspNetCore.Filters;
    using ProblemDetails = Be.Vlaanderen.Basisregisters.BasicApiProblem.ProblemDetails;

    [ApiVersion("1.0")]
    [AdvertiseApiVersions("1.0")]
    [ApiRoute("")]
    [ApiExplorerSettings(GroupName = "Home")]
    public class HomeController : ApiController
    {
        /// <summary>
        /// Initial entry point of the API.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(HomeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(HomeResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        public IActionResult GetHome() => Ok(new HomeResponse());

        [HttpGet("jsonorder")]
        [ProducesResponseType(typeof(JsonOrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(HomeResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        public IActionResult GetJsonOrder() => Ok(new JsonOrderResponse());
    }
}
