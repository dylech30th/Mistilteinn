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

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Mistilteinn;

public record Piece(Pieces Kind, Side Side);

[SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
public sealed partial class Chessboard
{
    public bool UseAutoCheckmateDetection { get; set; }
    
    public bool UseAutoStalemateDetection { get; set; }
    
    private Piece?[,] _board = new Piece?[9, 10];
    
    private Piece?[,] _lastMove = new Piece?[9, 10];

    private Side? _inCheck;
    
    public Chessboard()
    {
        InitChessboard();
    }

    private void InitChessboard()
    {
        (_board[0, 0], _board[8, 0]) = (new Piece(Pieces.Chariot, Side.Red), new Piece(Pieces.Chariot, Side.Red));
        (_board[1, 0], _board[7, 0]) = (new Piece(Pieces.Knight, Side.Red), new Piece(Pieces.Knight, Side.Red));
        (_board[2, 0], _board[6, 0]) = (new Piece(Pieces.Bishop, Side.Red), new Piece(Pieces.Bishop, Side.Red));
        (_board[3, 0], _board[5, 0]) = (new Piece(Pieces.Advisor, Side.Red), new Piece(Pieces.Advisor, Side.Red));
        _board[4, 0] = new Piece(Pieces.King, Side.Red);
        (_board[1, 2], _board[7, 2]) = (new Piece(Pieces.Cannon, Side.Red), new Piece(Pieces.Cannon, Side.Red));
        Enumerates.Range(0, 10, 2).ForEach(n => _board[n, 3] = new Piece(Pieces.Pawn, Side.Red));
        
        (_board[0, 9], _board[8, 9]) = (new Piece(Pieces.Chariot, Side.Black), new Piece(Pieces.Chariot, Side.Black));
        (_board[1, 9], _board[7, 9]) = (new Piece(Pieces.Knight, Side.Black), new Piece(Pieces.Knight, Side.Black));
        (_board[2, 9], _board[6, 9]) = (new Piece(Pieces.Bishop, Side.Black), new Piece(Pieces.Bishop, Side.Black));
        (_board[3, 9], _board[5, 9]) = (new Piece(Pieces.Advisor, Side.Black), new Piece(Pieces.Advisor, Side.Black));
        _board[4, 9] = new Piece(Pieces.King, Side.Black);
        (_board[1, 7], _board[7, 7]) = (new Piece(Pieces.Cannon, Side.Black), new Piece(Pieces.Cannon, Side.Black));
        Enumerates.Range(0, 10, 2).ForEach(n => _board[n, 6] = new Piece(Pieces.Pawn, Side.Black));
    }

    public IResult<ICheckResult[], string> Move(string instruction, Side side)
    {
        var replaced = ChineseNumeralsRegex().Replace(instruction, match => TextExtensions.TryParseChineseNumeral(match.Value[0], out var number) 
            ? number.ToString()
            : throw new ArgumentException("指令中包含非法的汉字数字"));
        if (side == Side.Black)
        {
            replaced = ChineseDirectionRegex().Replace(replaced, match => match.Value switch
            {
                "前" => "后",
                "后" => "前",
                "进" => "退",
                "退" => "进",
                _ => throw new ArgumentException("指令中包含非法的汉字方向")
            });
        }
        var parseResult = ParseInstruction(replaced, side);
        return parseResult.SelectMany(tuple => Move(tuple.x, tuple.y, tuple.toX, tuple.toY));
    }

    private IResult<(int x, int y, int toX, int toY), string> ParseInstruction(string instruction, Side side)
    {
        if (instruction.Length != 4)
            return IResult<(int x, int y, int toX, int toY), string>.Failure("指令长度不正确");

        if (instruction is [var pieceName, var fromX, var action, var dest])
        {
            // 前炮进一/后炮进一，此时 fromX 是棋子名
            if (pieceName is '前' or '后')
            {
                var pieceList = Enum.GetValues<Pieces>()
                    .Where(p => p.GetPieceName() is var (redSide, blackSide) && redSide == fromX || blackSide == fromX)
                    .ToList();
                if (!pieceList.Any())
                    return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的棋子名称不正确");
                if (!int.TryParse(dest.ToString(), out var t))
                    return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的目标位置不正确");
                if (!TextExtensions.TryParseMoveDirection(action, out var d))
                    return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的移动方向不正确");
                
                var p = pieceList.First();
                var pos = PositionOf(p, side).OrderBy(o => o.y).ToList();
                var (pieceX, pieceY) = pieceName is '前' ? pos.First() : pos.Last();

                // if p is Advisor or Knight or Bishop or direction is Horizontal
                // then t is the index of the column instead of the length of the steps, so we need to minus 1
                return p is Pieces.Advisor or Pieces.Knight or Pieces.Bishop || d is MoveTowards.Horizontal
                    ? GetFinalDestination(p, side, pieceX, pieceY, t - 1, d).Select(tuple => (pieceX, pieceY, tuple.x, tuple.y))
                    : GetFinalDestination(p, side, pieceX, pieceY, t, d).Select(tuple => (pieceX, pieceY, tuple.x, tuple.y));
            }
            var pieces = Enum.GetValues<Pieces>()
                .Where(p => p.GetPieceName() is var (redSide, blackSide) && redSide == pieceName || blackSide == pieceName)
                .ToList();
            if (!pieces.Any())
                return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的棋子名称不正确");
            if (!int.TryParse(fromX.ToString(), out var from))
                return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的起始位置不正确");
            if (!int.TryParse(dest.ToString(), out var to))
                return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的目标位置不正确");
            if (!TextExtensions.TryParseMoveDirection(action, out var direction))
                return IResult<(int x, int y, int toX, int toY), string>.Failure("指令中的移动方向不正确");
            
            var piece = pieces.First();
            if (PositionOf(piece, side).FirstOrDefault(p => p.x == from - 1) is var (x, y))
            {
                return piece is Pieces.Advisor or Pieces.Knight or Pieces.Bishop || direction is MoveTowards.Horizontal
                    ? GetFinalDestination(piece, side, x, y, to - 1, direction).Select(tuple => (x, y, tuple.x, tuple.y))
                    : GetFinalDestination(piece, side, x, y, to, direction).Select(tuple => (x, y, tuple.x, tuple.y));
            }
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"指令中的起始位置没有棋子'{piece.GetPieceName(side)}'");
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure("指令格式不正确");
    }

    private static IResult<(int x, int y), string> GetFinalDestination(Pieces piece, Side side, int currentX, int currentY, int dest, MoveTowards direction)
    {
        return piece switch
        {
            // 假设当前车位于第五列（中线）
            Pieces.Chariot => direction switch
            {
                // 车五进n
                MoveTowards.Forward => IResult<(int x, int y), string>.Unit((currentX, currentY + dest)),
                // 车五退n
                MoveTowards.Backward => IResult<(int x, int y), string>.Unit((currentX, currentY - dest)),
                // 车五平n
                MoveTowards.Horizontal => IResult<(int x, int y), string>.Unit((dest, currentY)),
                _ => IResult<(int x, int y), string>.Failure($"不符合{piece.GetPieceName(side)}的移动规则")
            },
            Pieces.Knight => direction switch
            {
                // 假设当前马位于第五列（中线）
                MoveTowards.Forward => (currentX - dest) switch
                {
                    // 马五进六/马五进四
                    -1 or 1 => IResult<(int x, int y), string>.Unit((dest, currentY + 2)),
                    // 马五进七/马五进三
                    -2 or 2 => IResult<(int x, int y), string>.Unit((dest, currentY + 1)),
                    _ => IResult<(int x, int y), string>.Failure($"不符合{piece.GetPieceName(side)}的移动规则")
                },
                MoveTowards.Backward => (currentX - dest) switch
                {
                    // 马五退六/马五退四
                    -1 or 1 => IResult<(int x, int y), string>.Unit((dest, currentY - 2)),
                    // 马五退七/马五退三
                    -2 or 2=> IResult<(int x, int y), string>.Unit((dest, currentY - 1)),
                    _ => IResult<(int x, int y), string>.Failure($"不符合{piece.GetPieceName(side)}的移动规则")
                },
                _ => IResult<(int x, int y), string>.Failure($"{piece.GetPieceName(side)}不能横向移动")
            },
            Pieces.Bishop => direction switch
            {
                // 假设当前象位于第五列（中线）
                MoveTowards.Forward when currentX - dest is 2 or -2 =>
                    // 象五进七/象五进三
                    IResult<(int x, int y), string>.Unit((dest, currentY + 2)),
                MoveTowards.Backward when currentX - dest is 2 or -2 => 
                    // 象五退七/象五退三
                    IResult<(int x, int y), string>.Unit((dest, currentY - 2)),
                _ => IResult<(int x, int y), string>.Failure($"{piece.GetPieceName(side)}不能横向移动")
            },
            Pieces.Advisor => direction switch
            {
                MoveTowards.Forward when currentX - dest is 1 or -1 =>
                    // 士五进六/士五进四
                    IResult<(int x, int y), string>.Unit((dest, currentY + 1)),
                MoveTowards.Backward when currentX - dest is 1 or -1 => 
                    // 士五退六/士五退四
                    IResult<(int x, int y), string>.Unit((dest, currentY - 1)),
                _ => IResult<(int x, int y), string>.Failure($"{piece.GetPieceName(side)}不能横向移动")
            },
            Pieces.King => direction switch
            {
                MoveTowards.Forward when dest == 1 => 
                    // 将五进一
                    IResult<(int x, int y), string>.Unit((currentX, currentY + 1)),
                MoveTowards.Backward when dest == 1 => 
                    // 将五退一
                    IResult<(int x, int y), string>.Unit((currentX, currentY - 1)),
                MoveTowards.Horizontal when dest - currentX is 1 or -1 => 
                    // 将五平六/将五平四
                    IResult<(int x, int y), string>.Unit((dest, currentY)),
                _ => IResult<(int x, int y), string>.Failure($"不符合{piece.GetPieceName(side)}的移动规则")
            },
            Pieces.Cannon => direction switch
            {
                // 炮五进n
                MoveTowards.Forward => IResult<(int x, int y), string>.Unit((currentX, currentY + dest)),
                // 炮五退n
                MoveTowards.Backward => IResult<(int x, int y), string>.Unit((currentX, currentY - dest)),
                // 炮五平n
                MoveTowards.Horizontal => IResult<(int x, int y), string>.Unit((dest, currentY)),
                _ => IResult<(int x, int y), string>.Failure($"不符合{piece.GetPieceName(side)}的移动规则")
            },
            Pieces.Pawn => direction switch
            {
                // 卒五进一
                MoveTowards.Forward when dest == 1 => IResult<(int x, int y), string>.Unit((currentX, currentY + 1)),
                MoveTowards.Backward when dest == 1 && side == Side.Black => IResult<(int x, int y), string>.Unit((currentX, currentY - 1)),
                // 卒五平六/卒五平四
                MoveTowards.Horizontal when dest - currentX is 1 or -1 => IResult<(int x, int y), string>.Unit((dest, currentY)),
                _ => IResult<(int x, int y), string>.Failure($"不符合{piece.GetPieceName(side)}的移动规则")
            },
            _ => IResult<(int x, int y), string>.Failure($"未知棋子: {piece.GetPieceName(side)}")
        };
    }
    
    // Move is separated into three phases:
    // 1. Check legality
    // 2. Perform the move
    // 3. Calculate the check (Additionally performs a "perpetual check" check to forbids endless chasing)
    // 4. (Optional) Announcing checkmate/draw/stalemate
    private IResult<ICheckResult[], string> Move(int x, int y, int toX, int toY)
    {
        var chess = _board[x, y];
        return chess is var (piece, side)
            ? DispatchRule(piece, side, x, y, toX, toY) 
            : IResult<ICheckResult[], string>.Failure("指定的位置没有棋子");
    }

    private IResult<ICheckResult[], string> DispatchRule(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        var moveResult = pieces switch
        {
            Pieces.Chariot => CheckChariot(pieces, side, x, y, toX, toY),
            Pieces.Knight => CheckKnight(pieces, side, x, y, toX, toY),
            Pieces.Bishop => CheckBishop(pieces, side, x, y, toX, toY),
            Pieces.Advisor => CheckAdvisor(pieces, side, x, y, toX, toY),
            Pieces.King => CheckKing(pieces, side, x, y, toX, toY),
            Pieces.Cannon => CheckCannon(pieces, side, x, y, toX, toY),
            Pieces.Pawn => CheckPawn(pieces, side, x, y, toX, toY),
            _ => throw new ArgumentOutOfRangeException(nameof(pieces), pieces, null)
        };
        return moveResult.SelectMany(_ =>
        {
            var board = (Piece?[,]) _board.Clone();
            var capture = _board[toX, toY];
            _board[x, y] = null;
            _board[toX, toY] = new Piece(pieces, side);
            var check = InCheck(side.Opponent());
            
            if (check is ICheckResult.CheckDelivered or ICheckResult.Checkmate)
            {
                _inCheck = side.Opponent();
            }
           
            // If the either side is in check at last round, and are still in check
            // after current move, withdraw the current move and issue a failure 
            // it generally means that the current move does not help to get out of the check
            if (InCheck(side) is ICheckResult.CheckDelivered or ICheckResult.Checkmate)
            {
                _board = board;
                if (side == _inCheck)
                    return IResult<ICheckResult[], string>.Failure($"{side.Name()}已被将军");
                return IResult<ICheckResult[], string>.Failure($"这样走会导致被将军");
            }

            if (side == _inCheck)
                _inCheck = null; // the side is no longer in check

            var list = new List<ICheckResult> { check };
            if (check is not ICheckResult.CheckDelivered and not ICheckResult.Checkmate &&
                UseAutoStalemateDetection &&
                StalemateDelivered(side.Opponent()))
                list.Add(new ICheckResult.Stalemate(side.Opponent()));
            if (capture is var (p, s)) 
                list.Add(new ICheckResult.Capture(x, y, toX, toY, pieces, side, p, s));
            if (check is ICheckResult.Moved) 
                list.Remove(check);

            list.Add(new ICheckResult.Moved(x, y, toX, toY, pieces, side));
            _lastMove = board;
            return IResult<ICheckResult[], string>.Unit(list.ToArray());
        });
    }
    
    private Piece?[] GetColumn(int x)
    {
        var row = new Piece?[10];
        for (var i = 0; i < 10; i++)
        {
            row[i] = _board[x, i];
        }

        return row;
    }
    
    private Piece?[] GetRow(int y)
    {
        var row = new Piece?[9];
        for (var i = 0; i < 9; i++)
        {
            row[i] = _board[i, y];
        }

        return row;
    }

    // 获取两个棋子之间的行或者列，如果 x 相等则获取列，否则如果 y 相等则获取行，不包含两端的棋子
    private IEnumerable<Piece?> GetInBetweenStraightExclusive(int x1, int y1, int x2, int y2)
    {
        if (x1 != x2 && y1 != y2) 
            throw new ArgumentException($"{x1} != {x2} && {y1} != {y2}");
        // + 1: Remove the piece at the end，the range operator ".." is 
        // inclusive-exclusive, which means the leftmost element is included
        // and the rightmost element is excluded. so we add 1 on its left element
        // to make it exclusive-exclusive.
        var result = x1 == x2
            ? GetColumn(x1)[Math.Min(y1, y2)..Math.Max(y1, y2)]
            : GetRow(y1)[Math.Min(x1, x2)..Math.Max(x1, x2)];
        return result is { Length: 0 } ? result : result[1..];
    }

    public IEnumerable<(int x, int y)> PositionOf(Pieces pieces, Side side)
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
            _board[toX, toY] = new Piece(pieces, side);

            var kingPosition = PositionOf(Pieces.King, side).First();
            var otherKingPosition = PositionOf(Pieces.King, side.Opponent()).First();

            return kingPosition.x == otherKingPosition.x && GetInBetweenStraightExclusive(kingPosition.x, kingPosition.y, otherKingPosition.x, otherKingPosition.y).All(p => p is null);
        });
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
        return x is >= 0 and <= 9 && y is >= 0 and <= 10;
    }

    private IResult<(int x, int y, int toX, int toY), string>? TrivialCheck(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        if (!CheckBound(toX, toY))
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能出界");
        if (!CheckDestination(x, y, toX, toY))
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能吃自己的棋子");
        if (flyingCheckmateCheck && CheckFlyingCheckmate(pieces, side, x, y, toX, toY))
            return IResult<(int x, int y, int toX, int toY), string>.Failure("禁止飞将！");
        return null;
    }
    
    private IResult<(int x, int y, int toX, int toY), string> CheckChariot(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        if (x != toX && y != toY)
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能直线移动");
        if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
            return failure;
        if (GetInBetweenStraightExclusive(x, y, toX, toY).Any(p => p is not null))
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}中间有棋子");
        return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckKnight(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (2, 1) or (1, 2))
        {
            if (Stuck())
                return IResult<(int x, int y, int toX, int toY), string>.Failure("马腿被堵住了");
            if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
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

    private IResult<(int x, int y, int toX, int toY), string> CheckBishop(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (2, 2))
        {
            if (AcrossBoundary()) 
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能过河");
            if (Stuck())
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}象眼被堵住了");
            if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能走田字");

        bool AcrossBoundary()
        {
            return side == Side.Red ? toY > 4 : toY < 5;
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

    private IResult<(int x, int y, int toX, int toY), string> CheckAdvisor(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (1, 1))
        {
            if (!InsidePalace(side, toX, toY))
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能在九宫格内");
            if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能走斜线");
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckKing(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        var xAbs = Math.Abs(toX - x);
        var yAbs = Math.Abs(toY - y);

        if ((xAbs, yAbs) is (1, 0) or (0, 1))
        {
            if (!InsidePalace(side, toX, toY))
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能在九宫格内");
            if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
                return failure;
            return IResult<(int x, int y, int toX, int toY), string>.Unit((x, y, toX, toY));
        }
        
        return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能走一格");
    }

    private IResult<(int x, int y, int toX, int toY), string> CheckCannon(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        if (x != toX && y != toY)
            return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}只能直线移动");
        if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
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

    private IResult<(int x, int y, int toX, int toY), string> CheckPawn(Pieces pieces, Side side, int x, int y, int toX, int toY, bool flyingCheckmateCheck = true)
    {
        var yDiff = toY - y;
        var xAbs = Math.Abs(toX - x);
        
        switch (yDiff, side)
        {
            case (> 1, Side.Red) or (< -1, Side.Black):
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}最多只能前进一格");
            case (< 0, Side.Red) or (> 0, Side.Black):
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}不能后退");
        }
        
        switch (xAbs)
        {
            case > 0 when WithinTerritory(side, y):
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}在己方领地内不能左右移动");
            case > 1:
                return IResult<(int x, int y, int toX, int toY), string>.Failure($"{pieces.GetPieceName(side)}最多只能左右移动一格");
        }
        
        if (TrivialCheck(pieces, side, x, y, toX, toY, flyingCheckmateCheck) is { } failure)
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
        var board = (Piece?[,]) _board.Clone();
        action();
        _board = board;
    }
    
    private T Recover<T>(Func<T> action)
    {
        var board = (Piece?[,]) _board.Clone();
        var result = action();
        _board = board;
        return result;
    }

    public void Retract()
    {
        _board = _lastMove;
    }

    [GeneratedRegex("一|二|三|四|五|六|七|八|九")]
    private static partial Regex ChineseNumeralsRegex();
    
    [GeneratedRegex("前|后|进|退")]
    private static partial Regex ChineseDirectionRegex();

    #region Automated Checkmate Detection

    // return `true` if `side` is in check, that is
    // it is targeted by the opponent's pieces
    private ICheckResult InCheck(Side side, bool checkmateCheck = true)
    {
        var king = PositionOf(Pieces.King, side).First();
        var ourChariot = PositionOf(Pieces.Chariot, side.Opponent());
        var ourKnight = PositionOf(Pieces.Knight, side.Opponent());
        var ourCanon = PositionOf(Pieces.Cannon, side.Opponent());
        var ourPawn = PositionOf(Pieces.Pawn, side.Opponent());
        
        // The flying checkmate (飞将) is before the other checks, since it is considered an illegal move
        var checkedByChariot = ourChariot.Select(tuple => CheckChariot(Pieces.Chariot, side.Opponent(), tuple.x, tuple.y, king.x, king.y, false))
            .FirstOrDefault(r => r.IsDefined);
        var checkedByKnight = ourKnight.Select(tuple => CheckKnight(Pieces.Knight, side.Opponent(), tuple.x, tuple.y, king.x, king.y, false))
            .FirstOrDefault(r => r.IsDefined);
        var checkedByCannon = ourCanon.Select(tuple => CheckCannon(Pieces.Cannon, side.Opponent(), tuple.x, tuple.y, king.x, king.y, false))
            .FirstOrDefault(r => r.IsDefined);
        var checkedByPawn = ourPawn.Select(tuple => CheckPawn(Pieces.Pawn, side.Opponent(), tuple.x, tuple.y, king.x, king.y, false))
            .FirstOrDefault(r => r.IsDefined);
        var checks = new[] { (Pieces.Chariot, checkedByChariot), (Pieces.Knight, checkedByKnight), (Pieces.Cannon, checkedByCannon), (Pieces.Pawn, checkedByPawn) }
            .Where(((Pieces, IResult<(int x, int y, int toX, int toY), string>? res) r) => r.res is not null)
            .Select(((Pieces p, IResult<(int x, int y, int toX, int toY), string>? res) r) => (r.p, r.res!.Get()))
            .ToList();
        
        if (checks.Any())
        {
            return checkmateCheck && UseAutoCheckmateDetection
                ? Checkmate(checks.Select(((Pieces p, (int x, int y, int toX, int toY) routes) r) => (r.p, (r.routes.x, r.routes.y))).ToList(), side) 
                    ? new ICheckResult.Checkmate(side)
                    : new ICheckResult.CheckDelivered(side)
                : new ICheckResult.CheckDelivered(side);
        }

        // to be filled in the future
        return new ICheckResult.Moved(-1, -1, -1, -1, 0, side.Opponent());
    }

    // returns `true` if the `side` is stalemated, that is
    // its king is not in check and cannot move to any other position
    private bool StalemateDelivered(Side side)
    {
        var king = PositionOf(Pieces.King, side).First();
        
        var forward = CheckKing(Pieces.King, side, king.x, king.y, king.x, king.y + 1, false);
        var backward = CheckKing(Pieces.King, side, king.x, king.y, king.x, king.y - 1, false);
        var left = CheckKing(Pieces.King, side, king.x, king.y, king.x - 1, king.y, false);
        var right = CheckKing(Pieces.King, side, king.x, king.y, king.x + 1, king.y, false);

        var moves = new[] { forward, backward, left, right };
        var checkResults = new List<ICheckResult>();
        foreach (var move in moves.OfType<Success<(int x, int y, int toX, int toY), string>>())
        {
            Recover(() =>
            {
                var (x, y, toX, toY) = move.Value;
                _board[x, y] = null;
                _board[toX, toY] = new Piece(Pieces.King, side);
                checkResults.Add(InCheck(side));
            });
        }

        return checkResults.All(r => r is not ICheckResult.Moved);
    }
    
    // returns `true` if `side` is checkmated
    // The checkmate detection detects in the following order:
    // 1. Detects whether the checkmate can be evaded by moving the king.
    // 2. Detects whether the checkmate can be evaded by blocking the piece that delivers the check.
    // 3. Detects whether the checkmate can be evaded by capturing the piece that delivers the check.
    // 4. If none of the above works, the checkmate is delivered and the game is over.
    private bool Checkmate(IReadOnlyCollection<(Pieces, (int x, int y))> checkPieces, Side side)
    {
        var (kingX, kingY) = PositionOf(Pieces.King, side).First();
        var forward = CheckKing(Pieces.King, side, kingX, kingY, kingX, kingY + 1, false);
        var backward = CheckKing(Pieces.King, side, kingX, kingY, kingX, kingY - 1, false);
        var left = CheckKing(Pieces.King, side, kingX, kingY, kingX - 1, kingY, false);
        var right = CheckKing(Pieces.King, side, kingX, kingY, kingX + 1, kingY, false);
        
        var movable = new[] { forward, backward, left, right }
            .Where(r => r.IsDefined)
            .Select(r => r.Get());
        var cannotMove = movable.All(m => Recover(() =>
        {
            _board[m.x, m.y] = null;
            _board[m.toX, m.toY] = new Piece(Pieces.King, side);

            return InCheck(side, false) is ICheckResult.CheckDelivered or ICheckResult.Checkmate;
        }));

        if (cannotMove)
        {
            if (checkPieces.Count > 1) return true;

            var (piece, (checkPieceX, checkPieceY)) = checkPieces.First();
            return !SpecializedStrategy(piece, side, checkPieceX, checkPieceY);
        }
        return false;
    }

    // This function trys to discover that are ad-hoc to the check piece to resolve the checkmate
    // using a set of heuristic steps.
    private bool SpecializedStrategy(Pieces checkPiece, Side side, int checkX, int checkY)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return checkPiece switch
        {
            // in the capture strategies, it is not required to check whether the king itself
            // can capture the checking piece, since it is already checked in the `InCheck` method
            // where the king is simulated to moves towards difference directions.
            Pieces.Chariot => ChariotStrategy(side, checkX, checkY),
            Pieces.Knight => KnightStrategy(side, checkX, checkY),
            Pieces.Pawn => PawnStrategy(side, checkX, checkY),
            Pieces.Cannon => CannonStrategy(side, checkX, checkY),
            _ => throw new ArgumentOutOfRangeException(nameof(checkPiece), checkPiece, null)
        };
    }

    // If the checkmate is caused by a cannon and the king cannot move to escape,
    // try to resolve it by a heuristic steps of strategy:
    // 1. If the support is of our own, try to remove it.
    // 2. If the cannon is in the same row or column as the king, try to block the cannon,
    //    this is proceeded as checking whether there are pieces that can reach the same
    //    row or column of the cannon without interfering with the cannon's support.
    // 3. If (2) cannot be done, try to capture the cannon by another piece. 
    //    This is proceeded as first check whether the cannon is within our own territory,
    //    and whether it is reachable from any of the bishops, this two checks eliminates
    //    redundant checks and saves the performance.
    // 4. If neither (1) nor (2) can be done, then the checkmate is inevitable.
    private bool CannonStrategy(Side side, int checkX, int checkY)
    {
        var (kingX, kingY) = PositionOf(Pieces.King, side).First();
        var support = GetInBetweenStraightExclusive(kingX, kingY, checkX, checkY).First(x => x is not null)!;
        var (supportX, supportY) = kingX == checkX
            ? PositionOf(support.Kind, support.Side).Single(p => p.x == kingX && p != (checkX, checkY))
            : PositionOf(support.Kind, support.Side).Single(p => p.y == kingY && p != (checkX, checkY));
        var exclude = kingX == checkX ? supportX : supportY;

        // If the support is of our own, try to remove it.
        if (support.Side == side)
        {
            return Recover(() =>
            {
                _board[supportX, supportY] = null;
                return InCheck(side, false) is not ICheckResult.CheckDelivered;
            });
        }

        // Block Strategies.
        if (ChariotBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY, exclude))
            return true;
        if (KnightBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY, exclude))
            return true;
        if (BishopBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY, exclude))
            return true;
        if (AdvisorBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY, exclude))
            return true;
        if (CannonBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY, exclude))
            return true;
        if (PawnBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY, exclude))
            return true;
        
        // Capture Strategies.
        if (CannonPointStrategy(side, checkX, checkY))
            return true;
        if (KnightPointStrategy(side, checkX, checkY))
            return true;
        if (BishopPointStrategy(side, checkX, checkY))
            return true;
        if (AdvisorPointStrategy(side, checkX, checkY))
            return true;
        if (PawnPointStrategy(side, checkX, checkY))
            return true;
        return false;
    }

    // Pawn checkmate resolution strategy:
    // 1. Capture the pawn.
    // 2. If (1) cannot be done, then the checkmate is inevitable.
    private bool PawnStrategy(Side side, int checkX, int checkY)
    {
        // Capture Strategies.
        if (ChariotPointStrategy(side, checkX, checkY))
            return true;
        if (KnightPointStrategy(side, checkX, checkY))
            return true;
        if (BishopPointStrategy(side, checkX, checkY))
            return true;
        if (AdvisorPointStrategy(side, checkX, checkY))
            return true;
        if (CannonPointStrategy(side, checkX, checkY))
            return true;
        if (PawnPointStrategy(side, checkX, checkY))
            return true;
        return false;
    }
    
    // Knight checkmate resolution strategy:
    // 1. Block the leg of the knight.
    // 2. Capture the knight.
    // 3. If neither (1) nor (2) can be done, then the checkmate is inevitable.
    private bool KnightStrategy(Side side, int checkX, int checkY)
    {
        var (kingX, kingY) = PositionOf(Pieces.King, side).First();
        var (legX, legY) = (kingX - checkX, kingY - checkY) switch
        {
            (-2, -1) or (-1, -2 ) => (kingX + 1, kingY + 1),
            (-2, 1) or (-1, 2) => (kingX + 1, kingY - 1),
            (2, -1) or (1, -2) => (kingX - 1, kingY + 1),
            (2, 1) or (1, 2) => (kingX - 1, kingY - 1),
            _ => throw new ArgumentOutOfRangeException()
        };
        
        // Block Strategies.
        if (ChariotPointStrategy(side, legX, legY))
            return true;
        if (KnightPointStrategy(side, legX, legY))
            return true;
        if (BishopPointStrategy(side, legX, legY))
            return true;
        if (AdvisorPointStrategy(side, legX, legY))
            return true;
        if (CannonPointStrategy(side, legX, legY))
            return true;
        if (PawnPointStrategy(side, legX, legY))
            return true;
        
        // Capture Strategies.
        if (ChariotPointStrategy(side, checkX, checkY))
            return true;
        if (KnightPointStrategy(side, checkX, checkY))
            return true;
        if (BishopPointStrategy(side, checkX, checkY))
            return true;
        if (AdvisorPointStrategy(side, checkX, checkY))
            return true;
        if (CannonPointStrategy(side, checkX, checkY))
            return true;
        if (PawnPointStrategy(side, checkX, checkY))
            return true;
        return false;
    }
    
    // Chariot checkmate resolution strategy:
    // 1. Tries to block the chariot.
    // 2. Tries to capture the chariot.
    // 3. If neither (1) nor (2) can be done, then the checkmate is inevitable.
    private bool ChariotStrategy(Side side, int checkX, int checkY)
    {
        var (kingX, kingY) = PositionOf(Pieces.King, side).First();
        // Block Strategies.
        if (ChariotBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY))
            return true;
        if (KnightBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY))
            return true;
        if (BishopBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY))
            return true;
        if (AdvisorBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY))
            return true;
        if (CannonBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY))
            return true;
        if (PawnBlockStrategy(side, kingX, kingY, checkX, checkY, kingY == checkY))
            return true;
        
        // Capture Strategies.
        if (ChariotPointStrategy(side, checkX, checkY))
            return true;
        if (CannonPointStrategy(side, checkX, checkY))
            return true;
        if (KnightPointStrategy(side, checkX, checkY))
            return true;
        if (BishopPointStrategy(side, checkX, checkY))
            return true;
        if (AdvisorPointStrategy(side, checkX, checkY))
            return true;
        if (PawnPointStrategy(side, checkX, checkY))
            return true;
        return false;
    }
    
    private bool PawnPointStrategy(Side side, int x, int y)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> chariotRoutes =
            from pawn in PositionOf(Pieces.Pawn, side)
            where pawn.x == x || pawn.y == y
            select (pawn.x, pawn.y, x, y);
        
        return chariotRoutes.Any(tuple => CheckPawn(Pieces.Pawn, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                          SimulateCheck(Pieces.Pawn, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }
    
    private bool PawnBlockStrategy(Side side, int kingX, int kingY, int checkX, int checkY, bool horizontal, int excludes = -1)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> pawnRoutes =
            from pawn in PositionOf(Pieces.Pawn, side)
            where horizontal 
                ? ((Math.Min(kingX, checkX) + 1)..Math.Max(kingX, checkX)).Contains(pawn.x)
                : ((Math.Min(kingY, checkY) + 1)..Math.Max(kingY, checkY)).Contains(pawn.y)
            where horizontal ? pawn.x != excludes : pawn.y != excludes
            select (pawn.x, pawn.y, horizontal ? pawn.x : checkX, horizontal ? checkY : pawn.y);

        return pawnRoutes.Any(tuple => CheckPawn(Pieces.Pawn, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined && 
                                       SimulateCheck(Pieces.Pawn, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }

    private bool CannonPointStrategy(Side side, int x, int y)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> chariotRoutes =
            from cannon in PositionOf(Pieces.Cannon, side)
            where cannon.x == x || cannon.y == y
            select (cannon.x, cannon.y, x, y);
        
        return chariotRoutes.Any(tuple => CheckCannon(Pieces.Cannon, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                          SimulateCheck(Pieces.Cannon, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }
    
    private bool CannonBlockStrategy(Side side, int kingX, int kingY, int checkX, int checkY, bool horizontal, int excludes = -1)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> chariotRoutes =
            from cannon in PositionOf(Pieces.Cannon, side)
            where horizontal 
                ? ((Math.Min(kingX, checkX) + 1)..Math.Max(kingX, checkX)).Contains(cannon.x)
                : ((Math.Min(kingY, checkY) + 1)..Math.Max(kingY, checkY)).Contains(cannon.y)
            where horizontal ? cannon.x != excludes : cannon.y != excludes
            select (cannon.x, cannon.y, horizontal ? cannon.x : checkX, horizontal ? checkY : cannon.y);

        return chariotRoutes.Any(tuple => CheckCannon(Pieces.Cannon, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                          SimulateCheck(Pieces.Cannon, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }

    private bool AdvisorPointStrategy(Side side, int x, int y)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> advisorRoutes =
            from advisor in PositionOf(Pieces.Advisor, side)
            select (advisor.x, advisor.y, x, y);
        
        return advisorRoutes.Any(tuple => CheckAdvisor(Pieces.Advisor, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined && 
                                          SimulateCheck(Pieces.Advisor, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.fromY));
    }
    
    private bool AdvisorBlockStrategy(Side side, int kingX, int kingY, int checkX, int checkY, bool horizontal, int excludes = -1)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> advisorRoutes =
            from advisor in PositionOf(Pieces.Advisor, side)
            from target in horizontal
                ? ((Math.Min(kingX, checkX) + 1)..Math.Max(kingX, checkX)).Enumerate().Where(x => x != excludes)
                : ((Math.Min(kingY, checkY) + 1)..Math.Max(kingY, checkY)).Enumerate().Where(y => y != excludes)
            select (advisor.x, advisor.y, horizontal ? target : checkX, horizontal ? checkY : target);

        return advisorRoutes.Distinct().Any(tuple => CheckAdvisor(Pieces.Advisor, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined && 
                                                     SimulateCheck(Pieces.Advisor, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }

    private bool BishopPointStrategy(Side side, int x, int y)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> bishopRoutes =
            from bishop in PositionOf(Pieces.Bishop, side)
            select (bishop.x, bishop.y, x, y);
        
        return bishopRoutes.Any(tuple => CheckBishop(Pieces.Bishop, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                          SimulateCheck(Pieces.Bishop, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }
    
    private bool BishopBlockStrategy(Side side, int kingX, int kingY, int checkX, int checkY, bool horizontal, int excludes = -1)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> bishopRoutes =
            from bishop in PositionOf(Pieces.Bishop, side)
            from target in horizontal
                ? ((Math.Min(kingX, checkX) + 1)..Math.Max(kingX, checkX)).Enumerate().Where(x => x != excludes)
                : ((Math.Min(kingY, checkY) + 1)..Math.Max(kingY, checkY)).Enumerate().Where(y => y != excludes)
            select (bishop.x, bishop.y, horizontal ? target : checkX, horizontal ? checkY : target);

        return bishopRoutes.Distinct().Any(tuple => CheckBishop(Pieces.Bishop, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                                    SimulateCheck(Pieces.Bishop, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }

    private bool KnightPointStrategy(Side side, int x, int y)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> knightRoutes =
            from knight in PositionOf(Pieces.Knight, side)
            select (knight.x, knight.y, x, y);
        
        return knightRoutes.Any(tuple => CheckKnight(Pieces.Knight, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                         SimulateCheck(Pieces.Knight, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }
    
    private bool KnightBlockStrategy(Side side, int kingX, int kingY, int checkX, int checkY, bool horizontal, int excludes = -1)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> knightRoutes =
            from knight in PositionOf(Pieces.Knight, side)
            from target in horizontal
                ? ((Math.Min(kingX, checkX) + 1)..Math.Max(kingX, checkX)).Enumerate().Where(x => x != excludes)
                : ((Math.Min(kingY, checkY) + 1)..Math.Max(kingY, checkY)).Enumerate().Where(y => y != excludes)
            select (knight.x, knight.y, horizontal ? target : checkX, horizontal ? checkY : target);

        return knightRoutes.Distinct().Any(tuple => CheckKnight(Pieces.Knight, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                                    SimulateCheck(Pieces.Knight, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }

    private bool ChariotPointStrategy(Side side, int x, int y)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> chariotRoutes =
            from chariot in PositionOf(Pieces.Chariot, side)
            where chariot.x == x || chariot.y == y
            select (chariot.x, chariot.y, x, y);
        
        return chariotRoutes.Any(tuple => CheckChariot(Pieces.Chariot, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                          SimulateCheck(Pieces.Chariot, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }
    
    private bool ChariotBlockStrategy(Side side, int kingX, int kingY, int checkX, int checkY, bool horizontal, int excludes = -1)
    {
        IEnumerable<(int fromX, int fromY, int toX, int toY)> chariotRoutes =
            from chariot in PositionOf(Pieces.Chariot, side)
            where horizontal 
                ? ((Math.Min(kingX, checkX) + 1)..Math.Max(kingX, checkX)).Contains(chariot.x)
                : ((Math.Min(kingY, checkY) + 1)..Math.Max(kingY, checkY)).Contains(chariot.y)
            where horizontal ? chariot.x != excludes : chariot.y != excludes
            select (chariot.x, chariot.y, horizontal ? chariot.x : checkX, horizontal ? checkY : chariot.y);

        return chariotRoutes.Any(tuple => CheckChariot(Pieces.Chariot, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY).IsDefined &&
                                          SimulateCheck(Pieces.Chariot, side, tuple.fromX, tuple.fromY, tuple.toX, tuple.toY));
    }

    private bool SimulateCheck(Pieces pieces, Side side, int x, int y, int toX, int toY)
    {
        return Recover(() =>
        {
            _board[x, y] = null;
            _board[toX, toY] = new Piece(pieces, side);
            return InCheck(side, false) is not ICheckResult.CheckDelivered;
        });
    }

    #endregion
}