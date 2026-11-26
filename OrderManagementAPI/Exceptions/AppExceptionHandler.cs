using Microsoft.AspNetCore.Diagnostics;

namespace OrderManagement.Exceptions;

public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException appEx)
            return false;

        context.Response.StatusCode = appEx.StatusCode;
        await context.Response.WriteAsJsonAsync(new { message = appEx.Message }, ct);
        return true;
    }
}
