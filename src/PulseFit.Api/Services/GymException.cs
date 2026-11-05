namespace PulseFit.Api.Services;

/// <summary>
/// A gym rule said no. The message is shown to the staff member as it is, so it says which rule and
/// what to do about it. The code lets the screen react to a particular case.
/// </summary>
public class GymRuleException : Exception
{
    public string Code { get; }
    public GymRuleException(string code, string message) : base(message) { Code = code; }
}

public class NotFoundException : Exception
{
    public NotFoundException(string what) : base($"{what} not found.") { }
}
