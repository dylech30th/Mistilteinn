using Mistilteinn;

var gameRunner = new GameRunner();
gameRunner.OnGameStateUpdated += async result =>
{
    if (!result.IsDefined)
    {
        Console.WriteLine("Error");
        return;
    }

    var r = result.Get();
    switch (r)
    {
        case IGameState.GameStart gs:
        {
            await using var fs = File.OpenWrite("test.png");
            await gs.InitialChessboard.CopyToAsync(fs);
            await gs.InitialChessboard.DisposeAsync();
            break;
        }
        case IGameState.PieceMoved pm:
        {
            await using var fs = File.OpenWrite("test.png");
            await pm.ImageStream.CopyToAsync(fs);
            await pm.ImageStream.DisposeAsync();
            break;
        }
    }

    Console.WriteLine(r);
};
await gameRunner.StartAsync();

while (true)
{
    await gameRunner.MoveAsync(Console.ReadLine()!);
}