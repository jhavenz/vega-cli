namespace VegaDevCli.Domain.Project;

public class VegaProjectNotFoundException : Exception
{
    public VegaProjectNotFoundException(string message) : base(message)
    {
    }

    public VegaProjectNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public VegaProjectNotFoundException(VegaProjectValidationResult validationResult) 
        : base(validationResult.GetFormattedErrorMessage())
    {
    }
}