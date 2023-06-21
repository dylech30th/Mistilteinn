#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/Chessboard.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Mistilteinn;

public class Chessboard
{
    private (Pieces piece, Side side)?[,] _board = new (Pieces, Side)?[9, 10];

    private bool _redSideInCheck;

    private bool _blackSideInCheck;

    // Move is separated into three phases:
    // 1. Check legality
    // 2. Perform the move
    // 3. Calculate the check (Additionally performs a "perpetual check" check to forbids endless chasing)
    // 4. (Optional) Announcing checkmate/draw/stalemate
    public IMoveResult Move(int x, int y, int toX, int toY)
    {
        var chess = _board[x, y];
        if (chess is var (piece, side))
        {
            
        }

        return new IMoveResult.Failure("指定的位置没有棋子");
    }

    private IResult<bool, string> DispatchRule(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var moveResult = pieces switch
        {
            Pieces.Chariot => CheckChariot(pieces, side, x, y, toX, toY),
            Pieces.Knight => CheckKnight(pieces, side, x, y, toX, toY),
            Pieces.Bishop => CheckBishop(pieces, side, x, y, toX, toY),
            Pieces.Advisor => CheckGuard(pieces, side, x, y, toX, toY),
            Pieces.King => CheckKing(pieces, side, x, y, toX, toY),
            Pieces.Cannon => CheckCannon(pieces, side, x, y, toX, toY),
            Pieces.Pawn => CheckPawn(pieces, side, x, y, toX, toY),
            _ => throw new ArgumentOutOfRangeException(nameof(pieces), pieces, null)
        };
        var checkResult = moveResult.Select(_ =>
        {
            _board[x, y] = null;
            _board[toX, toY] = (pieces, side);
            return CheckDelivered(side) switch
            {
                ICheckResult.Unchecked un => StalemateDelivered(side) ? new ICheckResult.Stalemate(side.Opponent()) : un,
                var result => result
            };
        });
        
        
    }
    
    private Pieces?[] GetColumn(int x)
    {
        var row = new Pieces?[10];
        for (var i = 0; i < 10; i++)
        {
            row[i] = _board[x, i]?.piece;
        }

        return row;
    }
    
    private Pieces?[] GetRow(int y)
    {
        var row = new Pieces?[9];
        for (var i = 0; i < 9; i++)
        {
            row[i] = _board[i, y]?.piece;
        }

        return row;
    }

    private IEnumerable<Pieces?> GetInBetweenStraightExclusive(int x1, int y1, int x2, int y2)
    {
        Contract.Requires(x1 == x2 || y1 == y2, "x1 == x2 || y1 == y2");
        if (x1 == x2)
        {
            if (y1 == y2 || Math.Abs(y1 - y2) is 1)
                return Array.Empty<Pieces?>();
            return GetColumn(x1)[(Math.Min(y1, y2) + 1)..Math.Max(y1, y2)];
        } 
        
        if (x1 == x2 || Math.Abs(x1 - x2) is 1)
            return Array.Empty<Pieces?>();
        return GetRow(y1)[(Math.Min(x1, x2) + 1)..Math.Min(x1, x2)];
    }

    private IEnumerable<(int x, int y)> PositionOf(Pieces pieces, Side side)
    {
        var coordinates = from x in Enumerable.Range(0, 9)
                          from y in Enumerable.Range(0, 10)
                          select (x, y);
        return coordinates.Where(tuple => _board[tuple.x, tuple.y] is var (p, s) && p == pieces && s == side);
    }

    // 飞将/照将, returns true if such checkmate happens
    private bool CheckFlyingCheckmate(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        _board[x, y] = null;
        _board[toX, toY] = (pieces, side);
        
        var kingPosition = PositionOf(Pieces.King, side).First();
        var otherKingPosition = PositionOf(Pieces.King, side.Opponent()).First();

        return kingPosition.x == otherKingPosition.x && GetInBetweenStraightExclusive(kingPosition.x, kingPosition.y, otherKingPosition.x, otherKingPosition.y).All(p => p is null);
    }
    
    // returns `false` if there are any pieces in between
    private bool CheckInBetween(int x, int y, int toX, int toY)
    {
        var between = GetInBetweenStraightExclusive(x, y, toX, toY);
        return between.All(p => p is null);
    }

    // returns `false` if destination is of the same side as the source
    private bool CheckDestination(int x, int y, int toX, int toY)
    {
        if (_board[x, y] is var (_, side))
        {
            Side? destinationSide = _board[toX, toY] is var (_, destSide) ? destSide : null;
            return destinationSide != side;
        }
        throw new ArgumentException("指定的位置没有棋子");
    }

    // returns `false` if the destination is out of bound
    private bool CheckBound(int x, int y)
    {
        return x is >= 0 and <= 9 || y is >= 0 and <= 10;
    }
    
    private ICheckResult CheckDelivered(Side ourSide)
    {
        var king = PositionOf(Pieces.King, ourSide.Opponent()).First();
        var ourChariot = PositionOf(Pieces.Chariot, ourSide);
        var ourKnight = PositionOf(Pieces.Knight, ourSide);
        var ourCanon = PositionOf(Pieces.Cannon, ourSide);
        var ourPawn = PositionOf(Pieces.Pawn, ourSide);
        
        // The detection for flying checkmate (飞将) is detect before the other checks, since it is considered an illegal move
        var checkedByChariot = ourChariot.Any(tuple => CheckChariot(Pieces.Chariot, ourSide, tuple.x, tuple.y, king.x, king.y).IsDefined);
        var checkedByKnight = ourKnight.Any(tuple => CheckKnight(Pieces.Knight, ourSide, tuple.x, tuple.y, king.x, king.y).IsDefined);
        var checkedByCanon = ourCanon.Any(tuple => CheckCannon(Pieces.Cannon, ourSide, tuple.x, tuple.y, king.x, king.y).IsDefined);
        var checkedByPawn = ourPawn.Any(tuple => CheckPawn(Pieces.Pawn, ourSide, tuple.x, tuple.y, king.x, king.y).IsDefined);
        var checks = new[] { checkedByChariot, checkedByKnight, checkedByCanon, checkedByPawn };
        
        if (checks.Any(Functions.Identity))
        {
            return Checkmate(ourSide) ? new ICheckResult.Checkmate(ourSide) : new ICheckResult.CheckDelivered(ourSide);
        }

        return new ICheckResult.Unchecked();
    }

    private bool StalemateDelivered(Side ourSide)
    {
        var board = _board;
        var king = PositionOf(Pieces.King, ourSide.Opponent()).First();
        
        var forward = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x, king.y + 1);
        var backward = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x, king.y - 1);
        var left = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x - 1, king.y);
        var right = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x + 1, king.y);

        var moves = new[] { forward, backward, left, right };
        var checkResults = new List<ICheckResult>();
        foreach (var move in moves.OfType<Success<(int x, int y, int toX, int toY), string>>())
        {
            var (x, y, toX, toY) = move.Value;
            _board[x, y] = null;
            _board[toX, toY] = (Pieces.King, ourSide.Opponent());
            checkResults.Add(CheckDelivered(ourSide));
            _board = board;
        }

        return checkResults.All(r => r is not ICheckResult.Unchecked);
    }
    
    private bool Checkmate(Side side)
    {
        var (kingX, kingY) = PositionOf(Pieces.King, side).First();
        var forward = CheckKing(Pieces.King, side, kingX, kingY, kingX, kingY + 1);
        var backward = CheckKing(Pieces.King, side, kingX, kingY, kingX, kingY - 1);
        var left = CheckKing(Pieces.King, side, kingX, kingY, kingX - 1, kingY);
        var right = CheckKing(Pieces.King, side, kingX, kingY, kingX + 1, kingY);
        return new[] { forward, backward, left, right }.All(r => !r.IsDefined);
    }
    
    private IResult<(int x, int y, int toX, int toY), string> CheckChariot(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        if (x != toX && y != toY)
        {
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName()}只能直线移动");
        }
        
        return null switch
        {
            _ when !CheckBound(toX, toY) =>
                IResult<(int x, int y, int toX, int toY), string>.Failure("目标位置超出棋盘"),
            _ when !CheckInBetween(x, y, toX, toY) =>
                IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName()}中间有棋子"),
            _ when !CheckDestination(x, y, toX, toY) =>
                IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName()}不能吃自己的棋子"),
            _ when CheckFlyingCheckmate(pieces, side, x, y, toX, toY) =>
                IResult<(int x, int y, int toX, int toY), string>.Failure("照将！"),
            _ => 
                IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY))
        };
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckKnight(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {

    }

    private IResult<(int x, int y, int toX, int toY), string> CheckBishop(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {

    }

    private IResult<(int x, int y, int toX, int toY), string> CheckGuard(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {

    }

    private IResult<(int x, int y, int toX, int toY), string> CheckKing(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {

    }

    private IResult<(int x, int y, int toX, int toY), string> CheckCannon(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {

    }

    private IResult<(int x, int y, int toX, int toY), string> CheckPawn(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {

    }
}