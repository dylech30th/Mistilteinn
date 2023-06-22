using Mistilteinn;

var chessboard = new Chessboard();
chessboard.Move(1, 2, 1, 1);
var res = chessboard.Move(1, 0, 2, 2);
Console.WriteLine(res);
