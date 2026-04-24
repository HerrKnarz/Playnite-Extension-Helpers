using Playnite;

namespace PlayniteExtensionHelpers.GamesCommon;

/// <summary>
/// Base class to be used to execute an action for a list of games. Needs to be inherited by the
/// actual action class.
/// </summary>
public abstract class BaseAction
{
    public virtual string Id { get; } = "base.action";
    public virtual string Name { get; } = "Base Action";

    /// <summary>
    /// Executes the action on all games in a blocking Task.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    public virtual async Task DoForAllAsync(BaseActionArgs args)
    {
        if (args.DebugMode)
        {
            Log.Debug($"===> Started {GetType()} for {args.Games.Count} games. =======================");
        }

        Cursor.Current = Cursors.WaitCursor;

        try
        {
            if (!await PrepareAsync(args))
            {
                return;
            }

            if (args.Games.Count == 1)
            {
                args.IsBulkAction = false;

                args.Games.First().NeedsToBeUpdated = await ExecuteAsync(args.Games.First(), args);

                await FollowUpAsync(args);
            }
            // if we have more than one game in the list, we want to start buffered mode and show a
            // progress bar.
            else if (args.Games.Count > 1)
            {
                var globalProgressOptions = new GlobalProgressOptions(
                    $"{args.PluginName} - {args.ProgressMessage}",
                    true
                )
                {
                    IsIndeterminate = false
                };

                await args.Api.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions,
                    async (globalProcessArgs) =>
                    {
                        try
                        {
                            globalProcessArgs.SetProgressMaxValue(args.Games.Count);

                            var counter = 0;

                            // TODO: Check if using UpdateAsync with the result of old and new data could be useful.
                            foreach (var game in args.Games)
                            {
                                globalProcessArgs.SetText($"{args.PluginName}{Environment.NewLine}{args.ProgressMessage}{Environment.NewLine}{game.Game?.Name}");

                                if (globalProcessArgs.CancelToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                game.NeedsToBeUpdated = game.Game is not null && await ExecuteAsync(game, args);

                                globalProcessArgs.SetCrrentProgressValue(++counter);
                            }

                            await FollowUpAsync(args);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    });

                if (!args.ShowDialogs)
                {
                    return;
                }

                Cursor.Current = Cursors.Default;
                await args.Api.Dialogs.ShowMessageAsync(args.Api.GetLocalizedString(args.ResultMessageId, ("gameCount", args.Games.Count(g => g.NeedsToBeUpdated))));
            }
        }
        finally
        {
            if (args.DebugMode)
            {
                Log.Debug($"===> Finished {GetType()} with {args.Games.Count(g => g.NeedsToBeUpdated)} games affected. =======================");
            }

            Cursor.Current = Cursors.Default;
        }
    }

    /// <summary>
    /// Executes the action on all games in a background Task.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    public virtual void DoForAllBackground(BaseActionArgs args)
        => args.Api.AddBackgroundOperation(new BaseActionBackgroundOp(args, PrepareAsync, ExecuteAsync, FollowUpAsync));

    /// <summary>
    /// DoForAll method that executes in a blocking thread for only one game but uses the background
    /// operation for multiple games.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    public virtual async Task DoForAllBackgroundOrAsync(BaseActionArgs args)
    {
        if (args.Games.Count == 1)
        {
            await DoForAllAsync(args);
        }
        else
        {
            DoForAllBackground(args);
        }
    }

    /// <summary>
    /// Executes the action on a game.
    /// </summary>
    /// <param name="game">The game to be processed</param>
    /// <param name="args">Arguments for the game action</param>
    /// <returns>true, if the action was successful</returns>
    public abstract Task<bool> ExecuteAsync(GameEx game, BaseActionArgs args);

    /// <summary>
    /// Executes follow-up steps after the execute method was run. Should be executed after a loop
    /// containing the Execute method.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    /// <returns>true, if the action was successful</returns>
    public virtual async Task FollowUpAsync(BaseActionArgs args)
    {
        if (args.GamesNeedUpdate)
        {
            await GameUpdater.UpdateGamesAsync(args.Games.Where(g => g.NeedsToBeUpdated).Select(g => g.Game).ToList(), args.Api, args.DebugMode);
        }
    }

    /// <summary>
    /// Creates an instance of the arguments needed to perform the action
    /// </summary>
    /// <param name="api">Instance of the playnite API</param>
    /// <param name="games">List of games the action will be executed for</param>
    /// <param name="pluginName">Name of the plugin</param>
    /// <returns>arguments to use in the action</returns>
    public virtual BaseActionArgs GetActionArgs(IPlayniteApi api, List<GameEx> games, string pluginName) => new(Id, Name, api, games, pluginName);

    /// <summary>
    /// Prepares the link action before performing the execute method. Should be executed before a
    /// loop containing the Execute method.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    /// <returns>true, if the action was successful</returns>
    public virtual async Task<bool> PrepareAsync(BaseActionArgs args) => true;
}