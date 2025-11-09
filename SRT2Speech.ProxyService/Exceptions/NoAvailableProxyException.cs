namespace SRT2Speech.ProxyService.Exceptions;

/// <summary>
/// Exception khi không có proxy nào available
/// </summary>
public class NoAvailableProxyException : ProxyException
{
    public NoAvailableProxyException() 
        : base("Không có proxy nào available để sử dụng")
    {
    }

    public NoAvailableProxyException(string message) : base(message)
    {
    }

    public NoAvailableProxyException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}