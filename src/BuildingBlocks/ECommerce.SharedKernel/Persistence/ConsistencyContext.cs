namespace ECommerce.SharedKernel.Persistence;

public sealed class ConsistencyContext
{
    public const string HeaderName = "X-Consistency-Token";

    public long? RequiredToken { get; set; }

    public long? WriteToken { get; set; }
}