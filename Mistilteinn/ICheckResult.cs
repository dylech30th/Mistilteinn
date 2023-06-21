namespace Mistilteinn;

public interface ICheckResult
{
    public sealed record CheckDelivered(Side InCheck) : ICheckResult;
    
    public sealed record Stalemate(Side Stalemated) : ICheckResult;
    
    public sealed record Checkmate(Side Checkmated) : ICheckResult;

    public sealed record Unchecked : ICheckResult;
}