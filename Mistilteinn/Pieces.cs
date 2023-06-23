#region Copyright (c) Mistilteinn/Mistilteinn
// GPL v3 License
// 
// Mistilteinn/Mistilteinn
// Copyright (c) 2023 Mistilteinn/Pieces.cs
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

public enum Pieces
{
    [PieceName('车', '车')]
    Chariot, // 車

    [PieceName('马', '马')]
    Knight, // 马

    [PieceName('相', '象')]
    Bishop, // 象

    [PieceName('士', '士')]
    Advisor, // 士

    [PieceName('帅', '将')]
    King, // 将

    [PieceName('炮', '炮')]
    Cannon, // 炮

    [PieceName('兵', '卒')]
    Pawn // 卒
}