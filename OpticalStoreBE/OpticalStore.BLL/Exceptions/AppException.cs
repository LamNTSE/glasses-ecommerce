using System.Net;

namespace OpticalStore.BLL.Exceptions;

public sealed class AppException : Exception
{
    public AppException(string errorCode, string message, HttpStatusCode statusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }

    public HttpStatusCode StatusCode { get; }
}
