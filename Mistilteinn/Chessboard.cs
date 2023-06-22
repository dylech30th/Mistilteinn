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
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace Mistilteinn;

[SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
public sealed class Chessboard
{
    private (Pieces piece, Side side)?[,] _board = new (Pieces, Side)?[9, 10];

    public Chessboard()
    {
        InitChessboard();
    }

    private void InitChessboard()
    {
        (_board[0, 0], _board[8, 0]) = ((Pieces.Chariot, Side.Red), (Pieces.Chariot, Side.Red));
        (_board[1, 0], _board[7, 0]) = ((Pieces.Knight, Side.Red), (Pieces.Knight, Side.Red));
        (_board[2, 0], _board[6, 0]) = ((Pieces.Bishop, Side.Red), (Pieces.Bishop, Side.Red));
        (_board[3, 0], _board[5, 0]) = ((Pieces.Advisor, Side.Red), (Pieces.Advisor, Side.Red));
        _board[4, 0] = (Pieces.King, Side.Red);
        (_board[1, 2], _board[7, 2]) = ((Pieces.Cannon, Side.Red), (Pieces.Cannon, Side.Red));
        Enumerates.Range(0, 10, 2).ForEach(n => _board[n, 3] = (Pieces.Pawn, Side.Red));
        
        (_board[0, 9], _board[8, 9]) = ((Pieces.Chariot, Side.Black), (Pieces.Chariot, Side.Black));
        (_board[1, 9], _board[7, 9]) = ((Pieces.Knight, Side.Black), (Pieces.Knight, Side.Black));
        (_board[2, 9], _board[6, 9]) = ((Pieces.Bishop, Side.Black), (Pieces.Bishop, Side.Black));
        (_board[3, 9], _board[5, 9]) = ((Pieces.Advisor, Side.Black), (Pieces.Advisor, Side.Black));
        _board[4, 9] = (Pieces.King, Side.Black);
        (_board[1, 7], _board[7, 7]) = ((Pieces.Cannon, Side.Black), (Pieces.Cannon, Side.Black));
        Enumerates.Range(0, 10, 2).ForEach(n => _board[n, 6] = (Pieces.Pawn, Side.Black));
    }
    
    // Move is separated into three phases:
    // 1. Check legality
    // 2. Perform the move
    // 3. Calculate the check (Additionally performs a "perpetual check" check to forbids endless chasing)
    // 4. (Optional) Announcing checkmate/draw/stalemate
    public IResult<ICheckResult, string> Move(int x, int y, int toX, int toY)
    {
        var chess = _board[x, y];
        return chess is var (piece, side)
            ? DispatchRule(piece, side, x, y, toX, toY) 
            : IResult<ICheckResult, string>.Failure("指定的位置没有棋子");
    }

    private IResult<ICheckResult, string> DispatchRule(Pieces pieces, Side side, int x, int y, int toX, int toY)
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
        return moveResult.Select(_ =>
        {
            var capture = _board[toX, toY];
            _board[x, y] = null;
            _board[toX, toY] = (pieces, side);
            return CheckDelivered(side) switch
            {
                ICheckResult.Unchecked un => StalemateDelivered(side) // 检查是否死局
                    ? new ICheckResult.Stalemate(side.Opponent()) 
                    : capture is var (p, s) // 检查本次移动是否吃子，如果是则返回不同的flag（ICheckResult.Capture）而不是 (ICheckResult.Unchecked)
                        ? new ICheckResult.Capture(x, y, toX, toY, p, s)
                        : un,
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

    // 获取两个棋子之间的行或者列，如果 x 相等则获取列，否则如果 y 相等则获取行，不包含两端的棋子
    private IEnumerable<Pieces?> GetInBetweenStraightExclusive(int x1, int y1, int x2, int y2)
    {
        Contract.Requires(x1 == x2 || y1 == y2, "x1 == x2 || y1 == y2");
        // + 1: Remove the piece at the end，the range operator ".." is 
        // inclusive-exclusive, which means the leftmost element is included
        // and the rightmost element is excluded. so we add 1 on its left element
        // to make it exclusive-exclusive.
        return x1 == x2
            ? GetColumn(x1)[(Math.Min(y1, y2) + 1)..Math.Max(y1, y2)]
            : GetRow(y1)[(Math.Min(x1, x2) + 1)..Math.Min(x1, x2)];
    }

    private IEnumerable<(int x, int y)> PositionOf(Pieces pieces, Side side)
    {
        // product of all possible `x` and `y`, thus effectively makes this all coordinates
        // of the chessboard. Ranging from (0, 0) to (8, 9)
        var coordinates = from x in Enumerable.Range(0, 9)
                          from y in Enumerable.Range(0, 10)
                          select (x, y);
        return coordinates.Where(tuple => _board[tuple.x, tuple.y] is var (p, s) && p == pieces && s == side);
    }

    // 飞将, returns true if such checkmate happens
    private bool CheckFlyingCheckmate(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        return Recover(() =>
        {
            _board[x, y] = null;
            _board[toX, toY] = (pieces, side);

            var kingPosition = PositionOf(Pieces.King, side).First();
            var otherKingPosition = PositionOf(Pieces.King, side.Opponent()).First();

            return kingPosition.x == otherKingPosition.x && GetInBetweenStraightExclusive(kingPosition.x, kingPosition.y, otherKingPosition.x, otherKingPosition.y).All(p => p is null);
        });
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
    private static bool CheckBound(int x, int y)
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
        
        // The flying checkmate (飞将) is before the other checks, since it is considered an illegal move
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
        var king = PositionOf(Pieces.King, ourSide.Opponent()).First();
        
        var forward = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x, king.y + 1);
        var backward = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x, king.y - 1);
        var left = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x - 1, king.y);
        var right = CheckKing(Pieces.King, ourSide.Opponent(), king.x, king.y, king.x + 1, king.y);

        var moves = new[] { forward, backward, left, right };
        var checkResults = new List<ICheckResult>();
        foreach (var move in moves.OfType<Success<(int x, int y, int toX, int toY), string>>())
        {
            Recover(() =>
            {
                var (x, y, toX, toY) = move.Value;
                _board[x, y] = null;
                _board[toX, toY] = (Pieces.King, ourSide.Opponent());
                checkResults.Add(CheckDelivered(ourSide));
            });
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

    private IResult<(int x, int y, int toX, int toY), string>? TrivialCheck(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        if (!CheckBound(toX, toY))
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能出界");
        if (!CheckDestination(x, y, toX, toY))
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能吃自己的棋子");
        if (CheckFlyingCheckmate(pieces, side, x, y, toX, toY))
            return IResult<(int x, int y, int toX, int toY), string>.Failure("照将！");
        return null;
    }
    
    private IResult<(int x, int y, int toX, int toY), string> CheckChariot(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        if (x != toX && y != toY)
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能直线移动");
        if (GetInBetweenStraightExclusive(x, y, toX, toY).Any(p => p is not null))
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}中间有棋子");
        if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
            return failure;
        return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckKnight(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (2, 1) or (1, 2))
        {
            if (Stuck())
                return IResult<(int x, int y, int toX, int toY), string>.Failure("马腿被堵住了");
            if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure("马只能走日字形");

        bool Stuck()
        {
            return (toX - x, toY - y) switch
            {
                // 别马腿
                (-2, _) => _board[x - 1, y] is not null,
                (2, _) => _board[x + 1, y] is not null,
                (_, -2) => _board[x, y - 1] is not null,
                (_, 2) => _board[x, y + 1] is not null,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckBishop(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (2, 2))
        {
            if (AcrossBoundary()) 
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能过河");
            if (Stuck())
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}象眼被堵住了");
            if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能走田字");

        bool AcrossBoundary()
        {
            return side == Side.Red ? toY <= 4 : toY >= 5;
        }
        
        bool Stuck()
        {
            // 塞象眼
            return (toX - x, toY - y) switch
            {
                (-2, 2) => _board[x - 1, y + 1] is not null,
                (2, 2) => _board[x + 1, y + 1] is not null,
                (-2, -2) => _board[x - 1, y - 1] is not null,
                (2, -2) => _board[x + 1, y - 1] is not null,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckGuard(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (1, 1))
        {
            if (!InsidePalace(side, toX, toY))
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能在九宫格内");
            if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能走斜线");
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckKing(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (1, 0) or (0, 1))
        {
            if (!InsidePalace(side, toX, toY))
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能在九宫格内");
            if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能走一格");
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckCannon(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        if (x != toX && y != toY)
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能直线移动");
        if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
            return failure;
        var destEmpty = _board[toX, toY] is null;
        var inBetween = GetInBetweenStraightExclusive(x, y, toX, toY).Count(p => p is not null);

        return (destEmpty, inBetween) switch
        {
            // The cannon is permitted to move if and only if the situation is of two cases:
            // 1. the target is empty, and between the cannon and the target there is no pieces
            // 2. the target is not empty, and between the cannon and the target there is seulement one piece.
            (true, 0) or (false, 1) => IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY)),
            (true, not 0) => IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}必须有一个目标"),
            (false, not 1) => IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}和目标中间应当有且仅有一个棋子")
        };
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckPawn(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var yDiff = toY - y;
        var xAbs = Math.Abs(toX - x);
        
        switch (yDiff)
        {
            case > 1:
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}最多只能前进一格");
            case < 1:
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能后退");
        }
        
        switch (xAbs)
        {
            case > 0 when WithinTerritory(side, y):
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}在己方领地内不能左右移动");
            case > 1:
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}最多只能左右移动一格");
        }
        
        if (TrivialCheck(pieces, side, x, y, toX, toY) is { } failure)
            return failure;
        return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
    }

    private static bool WithinTerritory(Side side, int y)
    {
        return side == Side.Red
            ? y <= 4
            : y >= 5;
    }
    
    private static bool InsidePalace(Side side, int toX, int toY)
    {
        return side == Side.Red
            ? (toX, toY) is (>= 3 and <= 5, >= 0 and <= 2)
            : (toX, toY) is (>= 3 and <= 5, >= 7 and <= 9);
    }

    private void Recover(Action action)
    {
        var board = ((Pieces, Side)?[,]) _board.Clone();
        action();
        _board = board;
    }
    
    private T Recover<T>(Func<T> action)
    {
        var board = ((Pieces, Side)?[,]) _board.Clone();
        var result = action();
        _board = board;
        return result;
    }
}