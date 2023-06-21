#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/MoveResult.cs
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

namespace Mistilteinn;

public interface IMoveResult
{
    public sealed record Move(int FromX, int FromY, int ToX, int ToY) : IMoveResult;

    public sealed record Capture(int FromX, int FromY, int ToX, int ToY, Pieces Captured, Side Side) : IMoveResult;

    public sealed record Checkmate(int FromX, int FromY, int ToX, int ToY, Side Checkmated) : IMoveResult;

    public sealed record InCheckFailure : IMoveResult;
}