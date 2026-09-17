namespace PiAiAssistant.Application.Exceptions;

public abstract class ApplicationExceptionBase : Exception
{
    protected ApplicationExceptionBase(string message) : base(message) { }
    protected ApplicationExceptionBase(string message, Exception inner) : base(message, inner) { }
}

public sealed class PiConnectionException : ApplicationExceptionBase
{
    public PiConnectionException(string message) : base(message) { }
    public PiConnectionException(string message, Exception inner) : base(message, inner) { }
}

public sealed class PiAuthenticationException : ApplicationExceptionBase
{
    public PiAuthenticationException(string message) : base(message) { }
    public PiAuthenticationException(string message, Exception inner) : base(message, inner) { }
}
