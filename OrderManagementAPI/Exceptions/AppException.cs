namespace OrderManagement.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }

    public object? Errors { get; init; }

    public AppException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public class NotFoundException(string message) : AppException(StatusCodes.Status404NotFound, message);

public class BadRequestException(string message) : AppException(StatusCodes.Status400BadRequest, message);

public class ConflictException(string message) : AppException(StatusCodes.Status409Conflict, message);
