namespace PiAiAssistant.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}

public sealed class TagNotFoundException : DomainException
{
    public string TagReference { get; }

    public TagNotFoundException(string tagReference)
        : base($"Tag '{tagReference}' was not found.")
    {
        TagReference = tagReference;
    }
}

public sealed class AmbiguousTagException : DomainException
{
    public string Query { get; }
    public int CandidateCount { get; }

    public AmbiguousTagException(string query, int candidateCount)
        : base($"Query '{query}' matched {candidateCount} tags; disambiguation required.")
    {
        Query = query;
        CandidateCount = candidateCount;
    }
}
