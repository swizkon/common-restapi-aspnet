﻿namespace Collector.Common.RestApi.AspNet
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http.Controllers;

    using Collector.Common.RestApi.AspNet.Infrastructure;
    using Collector.Common.RestContracts;

    using Newtonsoft.Json;

    using Serilog;

    public abstract class ErrorHandlingActionInvoker : ApiControllerActionInvoker
    {
        private readonly ILogger _logger;

        protected ErrorHandlingActionInvoker(ILogger logger)
        {
            _logger = logger.ForContext(GetType());
        }

        /// <summary>
        /// Asynchronously invokes the specified action by using the specified controller context.
        /// </summary>
        /// <param name="actionContext">The controller context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The invoked action.
        /// </returns>
        public override async Task<HttpResponseMessage> InvokeActionAsync(HttpActionContext actionContext, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var httpResponseMessage = await base.InvokeActionAsync(actionContext, cancellationToken);

                EnsureResponseCanBeSerialized(httpResponseMessage);

                return httpResponseMessage.IsSuccessStatusCode
                           ? httpResponseMessage
                           : CreateCustomHttpStatusCodeResponse(actionContext, httpResponseMessage.StatusCode);
            }
            catch (Exception e)
            {
                return CreateErrorResponse(actionContext, e);
            }
        }

        public HttpResponseMessage CreateErrorResponse(HttpActionContext actionContext, Exception exception)
        {
            var errorCode = GetErrorCode(exception);
            if (!string.IsNullOrEmpty(errorCode))
                return actionContext.Request.BuildUnprocessableEntityResponse(errorCode);

            LogException(actionContext, exception);

            return CreateCustomHttpStatusCodeResponse(actionContext, HttpStatusCode.InternalServerError);
        }

        protected abstract string GetErrorCode(Exception exception);

        private static HttpResponseMessage CreateCustomHttpStatusCodeResponse(HttpActionContext actionContext, HttpStatusCode httpStatusCode)
        {
            return actionContext.Request.CreateResponse(
                httpStatusCode,
                new Response<object>
                {
                    Error = new Error
                            {
                                Message = httpStatusCode.ToString(),
                                Code = $"{(int)httpStatusCode}"
                            }
                });
        }

        private static void EnsureResponseCanBeSerialized(HttpResponseMessage httpResponseMessage)
        {
            var objectContent = httpResponseMessage.Content as ObjectContent;
            if (objectContent != null)
                JsonConvert.SerializeObject(objectContent.Value);
        }

        private void LogException(HttpActionContext actionContext, Exception baseException)
        {
            _logger.Error(baseException, "Critical exception occured while processing request in controller {Controller}", actionContext.ControllerContext.ControllerDescriptor.ControllerName);
        }
    }
}