namespace Mistilteinn;

public enum GameOverReason
{
    Draw,
    Surrender,
    Checkmate
}

public interface IGameState
{
    public record PieceRetracted(Instruction RetractedInstruction) : IGameState;
        
    public record PieceMoved(Stream ImageStream, ICheckResult[] CheckResults) : IGameState;
    
    public record GameSuspended(Side Requester) : IGameState;
    
    public record GameResumed(Side Requester) : IGameState;
    
    public record GameStart(Stream InitialChessboard) : IGameState;
    
    public record GameOver(GameOverReason Reason, Side? Winner) : IGameState;
}

public record Instruction(string Text, int FromX, int FromY, int ToX, int ToY, Pieces? Captured, Side? CapturedSide, Side? CheckDelivered, Side? Checkmated, Side? Stalemated);

public sealed class GameRunner
{
    private Side? _drawRequested;
    private Side? _suspendRequested;
    private Side? _retractRequested;
    private bool? _gameSuspended;
    private bool _gameStarted;
    private bool _gameOver;
    private Side _currentSide;
    private Chessboard _chessboard;
    private readonly Queue<Instruction> _gameRecorder;
    
    public IReadOnlyCollection<Instruction> GameRecord => _gameRecorder;

    private Action<IResult<IGameState, string>>? _onGameStateUpdated;
    
    public event Action<IResult<IGameState, string>> OnGameStateUpdated
    {
        add => _onGameStateUpdated += value;
        remove => _onGameStateUpdated -= value;
    }

    public GameRunner()
    {
        _currentSide = Side.Red;
        _chessboard = new Chessboard();
        _gameRecorder = new Queue<Instruction>();
    }

    public async Task StartAsync()
    {
        _gameStarted = true;
        _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameStart(await Renderer.RenderAsync(_chessboard))));
    }

    public async Task MoveAsync(string instruction)
    {
        if (_retractRequested is not null)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("请先处理悔棋请求"));
            return;
        }
        
        if (!_gameStarted)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("游戏尚未开始"));
            return;
        }

        if (_gameSuspended is not null)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("暂停中的游戏无法进行走子"));
            return;
        }

        if (_gameOver)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("已结束的游戏无法进行走子"));
            return;
        }
        
        var res = _chessboard.Move(instruction, _currentSide);
        switch (res)
        {
            case Failure<ICheckResult[], string>(var error):
                _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure(error));
                return;
            case Success<ICheckResult[], string>(var results):
                RecordStep(instruction, results);
                if (results.Any(c => c is ICheckResult.Checkmate))
                {
                    var cm = results.OfType<ICheckResult.Checkmate>().First();
                    _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameOver(GameOverReason.Checkmate, cm.Checkmated.Opponent())));
                    Close();
                    return;
                }

                if (results.Any(c => c is ICheckResult.Stalemate))
                {
                    var sm = results.OfType<ICheckResult.Stalemate>().First();
                    _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameOver(GameOverReason.Draw, sm.Stalemated.Opponent())));
                    Close();
                    return;
                }

                _currentSide = _currentSide.Opponent();
                var moved = results.OfType<ICheckResult.Moved>().First();
                var image = await Renderer.RenderAsync(_chessboard, moved.ToX, moved.ToY);
                _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.PieceMoved(image, results)));
                return;
        }
    }

    private void RecordStep(string instruction, IReadOnlyCollection<ICheckResult> results)
    {
        var ins = new Instruction(instruction, -1, -1, -1, -1, null, null, null, null, null);
        if (results.OfType<ICheckResult.Moved>().ToArray() is [var (fromX, fromY, toX, toY, _, _), ..])
            ins = ins with { FromX = fromX, FromY = fromY, ToX = toX, ToY = toY };
        if (results.OfType<ICheckResult.Capture>().ToArray() is [var (_, _, _, _, _, _, captured, side), ..])
            ins = ins with { Captured = captured, CapturedSide = side };
        if (results.OfType<ICheckResult.CheckDelivered>().ToArray() is [var cd, ..])
            ins = ins with { CheckDelivered = cd.InCheck };
        if (results.OfType<ICheckResult.Checkmate>().ToArray() is [var cm, ..])
            ins = ins with { Checkmated = cm.Checkmated };
        if (results.OfType<ICheckResult.Stalemate>().ToArray() is [var sm, ..])
            ins = ins with { Stalemated = sm.Stalemated };
        _gameRecorder.Enqueue(ins);
    }

    public void RequestDraw()
    {
        _drawRequested = _currentSide;
    }

    public void AcceptDraw()
    {
        if (_drawRequested is null)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("无法接受未请求的和棋"));
            return;
        }
        
        if (_drawRequested == _currentSide)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("无法和自己请求和棋"));
            return;
        }
        
        _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameOver(GameOverReason.Draw, _currentSide)));
        Close();
    }
    
    public void RequestSuspension() => _suspendRequested = _currentSide;

    public void AcceptSuspension()
    {
        if (_suspendRequested == null)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("无法接受未请求的暂停"));
            return;
        }
        
        if (_suspendRequested == _currentSide)
        {
            _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Failure("无法暂停自己的回合"));
            return;
        }
        _gameSuspended = true;
        _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameSuspended(_currentSide)));
    }
    
    public void Surrender()
    {
        _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameOver(GameOverReason.Surrender, _currentSide)));
        Close();
    }

    public void Resume()
    {
        _gameSuspended = false;
        _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.GameResumed(_currentSide)));
    }

    public void RequestRetraction()
    {
        _retractRequested = _currentSide;
    }
    
    public void AcceptRetraction()
    {
        if (_retractRequested is null) return;
        if (_gameRecorder.Count == 0) return;
        var ins = _gameRecorder.Dequeue();
        _chessboard.Retract();
        _currentSide = _currentSide.Opponent();
        _onGameStateUpdated?.Invoke(IResult<IGameState, string>.Unit(new IGameState.PieceRetracted(ins)));
    }

    public void DisableAutoCheckmateDetection() => _chessboard.UseAutoCheckmateDetection = false;

    public void DisableAutoStalemateDetection() => _chessboard.UseAutoStalemateDetection = false;

    public void UseAutoCheckmateDetection() => _chessboard.UseAutoCheckmateDetection = true;

    public void UseAutoStalemateDetection() => _chessboard.UseAutoStalemateDetection = true;

    // ReSharper disable once MemberCanBePrivate.Global
    public void Close()
    {
        _chessboard = null!;
        _gameOver = true;
        _onGameStateUpdated = null!;
    }
}