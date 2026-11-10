namespace ECommerce.SharedKernel.Persistence;

public sealed class ReplicaCircuitBreaker
{
    private const int Threshold = 3;
    private static readonly TimeSpan OpenFor = TimeSpan.FromSeconds(30);
    private readonly object gate = new();
    private int failures;
    private DateTime openUntilUtc = DateTime.MinValue;

    public bool IsOpen { get { lock (gate) return DateTime.UtcNow < openUntilUtc; } }

    public void Success() { lock (gate) failures = 0; }

    public void Failure()
    {
        lock (gate)
        {
            if (++failures >= Threshold)
                openUntilUtc = DateTime.UtcNow + OpenFor;
        }
    }
}