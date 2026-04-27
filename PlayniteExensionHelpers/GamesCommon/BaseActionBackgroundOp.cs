using Playnite;

namespace PlayniteExtensionHelpers.GamesCommon;

public class BaseActionBackgroundOp : BackgroundOperation
{
    private readonly BaseActionArgs _actionArgs;
    private readonly CancellationTokenSource _cancelToken = new();
    private readonly Func<BaseActionGame, BaseActionArgs, Task<bool>> _executeFunc;
    private readonly Func<BaseActionArgs, Task> _followUpFunc;
    private readonly Func<BaseActionArgs, Task<bool>> _prepareFunc;
    private readonly Func<Game, BaseActionGame, bool> _updateGameFunc;

    private IGlobalProgressActionArgs? _globalProgressArgs;

    public BaseActionBackgroundOp(BaseActionArgs args,
            Func<BaseActionArgs, Task<bool>> prepareFunc,
        Func<BaseActionGame, BaseActionArgs, Task<bool>> executeFunc,
        Func<BaseActionArgs, Task> followUpFunc,
        Func<Game, BaseActionGame, bool> updateGameFunc) : base(args.Id, $"{args.PluginName}: {args.Name}")
    {
        Pausable = false;
        _actionArgs = args;
        _prepareFunc = prepareFunc;
        _executeFunc = executeFunc;
        _followUpFunc = followUpFunc;
        _updateGameFunc = updateGameFunc;
    }

    public override async ValueTask DisposeAsync()
    {
        _cancelToken.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Executes the action on all games in a blocking Task.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    public virtual async Task DoForAllAsync()
    {
        if (_actionArgs.DebugMode)
        {
            Log.Debug($"===> Started {_actionArgs.Name} for {_actionArgs.Games.Count} games. =======================");
        }

        Cursor.Current = Cursors.WaitCursor;

        try
        {
            try
            {
                if (!await _prepareFunc(_actionArgs))
                {
                    return;
                }

                if (_actionArgs.Games.Count == 1)
                {
                    _actionArgs.IsBulkAction = false;

                    _actionArgs.Games.First().NeedsToBeUpdated = await _executeFunc(_actionArgs.Games.First(), _actionArgs);

                    await FollowUpAsync();

                    await _followUpFunc(_actionArgs);
                }
                // if we have more than one game in the list, we want to show a progress bar.
                else if (_actionArgs.Games.Count > 1)
                {
                    var globalProgressOptions = new GlobalProgressOptions(
                        $"{_actionArgs.PluginName} - {_actionArgs.ProgressMessage}",
                        true
                    )
                    {
                        IsIndeterminate = false
                    };

                    await _actionArgs.Api.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions,
                        async (globalProgressArgs) =>
                        {
                            try
                            {
                                globalProgressArgs.SetProgressMaxValue(_actionArgs.Games.Count);

                                var counter = 0;

                                if (_actionArgs.DoForAllType == DoForAllTypes.BlockingLoop)
                                {
                                    foreach (var game in _actionArgs.Games)
                                    {
                                        globalProgressArgs.SetText($"{_actionArgs.PluginName}{Environment.NewLine}{_actionArgs.ProgressMessage}{Environment.NewLine}{game.Game?.Name}");

                                        if (globalProgressArgs.CancelToken.IsCancellationRequested)
                                        {
                                            break;
                                        }

                                        game.NeedsToBeUpdated = game.Game is not null && await _executeFunc(game, _actionArgs);

                                        globalProgressArgs.SetCurrentProgressValue(++counter);
                                    }

                                    await FollowUpAsync();
                                }
                                else
                                {
                                    _globalProgressArgs = globalProgressArgs;
                                    try
                                    {
                                        // NEXT: Implement for all actions set to BlockingBulkUpdate
                                        await _actionArgs.Api.Library.Games.UpdateAsync(
                                            [.. _actionArgs.Games.Where(g => g.NeedsToBeUpdated).Select(g => g.Game.Id)],
                                            async (g) => await _executeFunc(new BaseActionGame(g), _actionArgs));
                                    }
                                    finally
                                    {
                                        _globalProgressArgs = null;
                                    }
                                }

                                await _followUpFunc(_actionArgs);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex);
                            }
                        });

                    if (!_actionArgs.ShowDialogs)
                    {
                        return;
                    }

                    Cursor.Current = Cursors.Default;
                    await _actionArgs.Api.Dialogs.ShowMessageAsync(_actionArgs.Api.GetLocalizedString(_actionArgs.ResultMessageId, ("gameCount", _actionArgs.Games.Count(g => g.NeedsToBeUpdated))));
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        finally
        {
            if (_actionArgs.DebugMode)
            {
                Log.Debug($"===> Finished {_actionArgs.Name} with {_actionArgs.Games.Count(g => g.NeedsToBeUpdated)} games affected. =======================");
            }

            Cursor.Current = Cursors.Default;
        }
    }

    public virtual async Task FollowUpAsync()
    {
        if (_actionArgs.GamesNeedUpdate)
        {
            await _actionArgs.Api.Library.Games.UpdateAsync([.. _actionArgs.Games.Where(g => g.NeedsToBeUpdated).Select(g => g.Game.Id)], UpdateInDbInFollowUp);
        }
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

                    //NEXT: Make background operations pause-able!
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

                    await FollowUpAsync();

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

    /// <summary>
    /// Updates the game directly in the database after it was processed in the loop. Should only be
    /// called in an UpdateAsync method of the game library. The game will only be updated, if the
    /// method returns true.
    /// </summary>
    /// <param name="game">Game to update</param>
    /// <returns>true, if the game needs to be updated.</returns>
    public virtual bool UpdateInDbInFollowUp(Game game)
    {
        var processedGame = _actionArgs?.Games.FirstOrDefault(g => g.GameId.Equals(game.Id));

        return processedGame is not null && _updateGameFunc(game, processedGame);
    }
}