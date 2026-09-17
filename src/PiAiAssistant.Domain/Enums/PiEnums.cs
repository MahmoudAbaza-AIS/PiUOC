namespace PiAiAssistant.Domain.Enums;

public enum TagResolutionStatus
{
    Found,
    NotFound,
    Ambiguous,
    Forbidden,
    Error
}

public enum PiAuthenticationMode
{
    Demo,
    Anonymous,
    Basic,
    Windows,
    DefaultCredentials,
    NetworkCredential,
    Bearer
}
