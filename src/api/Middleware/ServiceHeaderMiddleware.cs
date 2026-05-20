namespace Api.Middleware;

/// <summary>
/// Adds the <c>X-Service: api</c> response header to every outbound response (AC-002).
/// </summary>
public sealed class ServiceHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public ServiceHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Service"] = "api";
        await _next(context);
    }
}
