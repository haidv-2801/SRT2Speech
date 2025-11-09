namespace SRT2Speech.ProxyService.Exceptions;

/// <summary>
/// Exception khi có lỗi trong cấu hình proxy
/// </summary>
public class ProxyConfigurationException : ProxyException
{
    public ProxyConfigurationException() 
        : base("Lỗi trong cấu hình proxy")
    {
    }

    public ProxyConfigurationException(string message) : base(message)
    {
    }

    public ProxyConfigurationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}