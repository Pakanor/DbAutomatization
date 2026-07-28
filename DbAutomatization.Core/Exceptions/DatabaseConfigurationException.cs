namespace DbAutomatization.Core.Exceptions;

public class DatabaseConfigurationException : Exception
{
    public DatabaseConfigurationException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
