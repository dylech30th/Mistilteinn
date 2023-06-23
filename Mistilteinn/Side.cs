#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/Side.cs
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

public enum Side
{
    Red,
    Black
}

public static class SideExtension
{
    public static Side Opponent(this Side side) => side == Side.Black ? Side.Red : Side.Black;

    public static string Name(this Side side) => side switch
    {
        Side.Red => "红方",
        Side.Black => "黑方",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };
}