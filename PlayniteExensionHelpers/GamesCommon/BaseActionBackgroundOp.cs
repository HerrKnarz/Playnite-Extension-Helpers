using Playnite;

namespace PlayniteExtensionHelpers.GamesCommon;

public class BaseActionBackgroundOp : BackgroundOperation
{
    private readonly BaseActionArgs _actionArgs;
    private readonly CancellationTokenSource _cancelToken = new();
    private readonly Func<GameEx, BaseActionArgs, Task<bool>> _executeFunc;
    private readonly Func<BaseActionArgs, Task> _followUpFunc;

    private readonly Func<BaseActionArgs, Task<bool>> _prepareFunc;

    public BaseActionBackgroundOp(BaseActionArgs args,
        Func<BaseActionArgs, Task<bool>> prepareFunc,
        Func<GameEx, BaseActionArgs, Task<bool>> executeFunc,
        Func<BaseActionArgs, Task> followUpFunc) : base(args.Id, $"{args.PluginName}: {args.Name}")
    {
        Pausable = false;
        _actionArgs = args;
        _prepareFunc = prepareFunc;
        _executeFunc = executeFunc;
        _followUpFunc = followUpFunc;
    }

    public override async ValueTask DisposeAsync()
    {
        _cancelToken.Dispose();
        GC.SuppressFinalize(this);
    }

    public override async Task StartAsync(StartArgs args)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                try
                {
                    if (_actionArgs.DebugMode)
                    {
                        Log.Debug($"===> Started {_actionArgs.Id} for {_actionArgs.Games.Count} games. =======================");
                    }

                    Status = _actionArgs.ProgressMessage;
                    ProgressIsIndeterminate = false;
                    ProgressMaximum = _actionArgs.Games.Count;

                    if (!await _prepareFunc(_actionArgs))
                    {
                        return;
                    }

                    var counter = 0;

                    foreach (var game in _actionArgs.Games)
                    {
                        Status = $"{_actionArgs.ProgressMessage}{Environment.NewLine}{game.Game?.Name}";

                        if (_cancelToken.IsCancellationRequested)
                        {
                            break;
                        }

                        game.NeedsToBeUpdated = game.Game is not null && await _executeFunc(game, _actionArgs);

                        ProgressValue = ++counter;
                    }

                    await _followUpFunc(_actionArgs);

                    Status = _actionArgs.Api.GetLocalizedString(_actionArgs.ResultMessageId, ("gameCount", _actionArgs.Games.Count(g => g.NeedsToBeUpdated)));

                    await OperationFinishedAsync(new FinishedEventArgs());
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    await OperationFailedAsync(new FailedEventArgs(e.Message));
                }
            }
            finally
            {
                if (_actionArgs.DebugMode)
                {
                    Log.Debug($"===> Finished {_actionArgs.Id} with {_actionArgs.Games.Count(g => g.NeedsToBeUpdated)} games affected. =======================");
                }
            }
        });
    }

    public override async Task StopAsync(StopArgs args) => await _cancelToken.CancelAsync();
}