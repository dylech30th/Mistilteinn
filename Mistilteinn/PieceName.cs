#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/PieceName.cs
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

using System.Reflection;

namespace Mistilteinn;

public sealed class PieceName : Attribute
{
    public char RedSide { get; }

    public char BlackSide { get; }

    public PieceName(char redSide, char blackSide)
    {
        RedSide = redSide;
        BlackSide = blackSide;
    }
}

public static class PieceNameExtension
{
    public static (char redSide, char blackSide) GetPieceName(this Pieces piece)
    {
        var metadata = typeof(Pieces).GetField(piece.ToString())!.GetCustomAttribute<PieceName>()!;
        return (metadata.RedSide, metadata.BlackSide);
    }
}