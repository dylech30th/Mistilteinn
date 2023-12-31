﻿namespace Mistilteinn;

public interface ICheckResult
{
    // 将军
    public sealed record CheckDelivered(Side InCheck) : ICheckResult;
    
    // 死局
    public sealed record Stalemate(Side Stalemated) : ICheckResult;
    
    // 将死
    public sealed record Checkmate(Side Checkmated) : ICheckResult;
    
    // 正常走子，未将军
    public sealed record Moved(int FromX, int FromY, int ToX, int ToY, Pieces Piece, Side Side) : ICheckResult;
    
    // 正常走子，吃子
    public sealed record Capture(int FromX, int FromY, int ToX, int ToY, Pieces Capturer, Side CapturerSide, Pieces Captured, Side Side) : ICheckResult;
}