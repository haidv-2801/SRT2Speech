namespace SRT2Speech.ProxyService.Exceptions;

/// <summary>
/// Base exception cho tất cả proxy-related errors
/// </summary>
public class ProxyException : Exception
{
    public ProxyException() : base()
    {
    }

    public ProxyException(string message) : base(message)
    {
    }

    public ProxyException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}