namespace OpenClaw.Core.CloudSync;

/// <summary>
/// Thin ASP.NET glue mapping <c>POST /graph/notifications</c> to
/// <see cref="NotificationRequestProcessor"/> (D-6): query/body in, processor result
/// out. All validation, comparison, and enqueue logic lives in the processor.
/// </summary>
internal static class GraphNotificationsEndpoint
{
    /// <summary>
    /// Maps the Graph notifications webhook route. Called by the composition root only
    /// when <c>OpenClaw:CloudSync:Enabled</c> is <c>true</c>.
    /// </summary>
    /// <param name="endpoints">The route builder to map onto.</param>
    internal static IEndpointRouteBuilder MapGraphNotificationsEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapPost(
            "/graph/notifications",
            async (HttpContext context, NotificationRequestProcessor processor) =>
            {
                string? validationToken = context.Request.Query["validationToken"];
                NotificationProcessorResult result;
                if (!string.IsNullOrEmpty(validationToken))
                {
                    result = NotificationRequestProcessor.HandleHandshake(validationToken);
                }
                else
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync(context.RequestAborted);
                    result = await processor.ProcessNotificationsAsync(
                        body,
                        context.RequestAborted
                    );
                }

                context.Response.StatusCode = result.StatusCode;
                if (result.ContentType is not null)
                {
                    context.Response.ContentType = result.ContentType;
                }

                if (result.Body is not null)
                {
                    await context.Response.WriteAsync(result.Body, context.RequestAborted);
                }
            }
        );
        return endpoints;
    }
}
