using Microsoft.AspNetCore.Diagnostics;

namespace OrderManagement.Exceptions;

public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException appEx)
            return false;

        context.Response.StatusCode = appEx.StatusCode;
        object body = appEx.Errors is null
            ? new { message = appEx.Message }
            : new { message = appEx.Message, errors = appEx.Errors };
        await context.Response.WriteAsJsonAsync(body, ct);
        return true;
    }
}
