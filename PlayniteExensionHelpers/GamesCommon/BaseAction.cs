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
    //NEXT: Add option to do the game loop as an UpdateAsync for fast running actions

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
            var blockingOp = new BaseActionBackgroundOp(args, PrepareAsync, ExecuteAsync, FollowUpAsync, ProcessUpdateData);

            await blockingOp.DoForAllAsync();
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
    // TODO: If possible check if the same operation is still running and ask if the user still wants to add another one to the list.
    public virtual void DoForAllBackground(BaseActionArgs args)
        => args.Api.AddBackgroundOperation(new BaseActionBackgroundOp(args, PrepareAsync, ExecuteAsync, FollowUpAsync, ProcessUpdateData));

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
    public abstract Task<bool> ExecuteAsync(BaseActionGame game, BaseActionArgs args);

    /// <summary>
    /// Executes follow-up steps after the execute method was run. Should be executed after a loop
    /// containing the Execute method.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    /// <returns>true, if the action was successful</returns>
    public virtual async Task FollowUpAsync(BaseActionArgs args)
    { }

    /// <summary>
    /// Creates an instance of the arguments needed to perform the action
    /// </summary>
    /// <param name="api">Instance of the playnite API</param>
    /// <param name="games">List of games the action will be executed for</param>
    /// <param name="pluginName">Name of the plugin</param>
    /// <returns>arguments to use in the action</returns>
    public virtual BaseActionArgs GetActionArgs(IPlayniteApi api, List<BaseActionGame> games, string pluginName) => new(Id, Name, api, games, pluginName);

    /// <summary>
    /// Prepares the link action before performing the execute method. Should be executed before a
    /// loop containing the Execute method.
    /// </summary>
    /// <param name="args">Arguments for the game action</param>
    /// <returns>true, if the action was successful</returns>
    public virtual async Task<bool> PrepareAsync(BaseActionArgs args) => true;

    /// <summary>
    /// Update the database record for the specified game using values from the processed game.
    /// </summary>
    /// <remarks>
    /// This method gets called in UpdateInDb, where it will result in an actual update of the game
    /// in the library. It should only update the fields in the game, but not call UpdateAsync itself.
    /// </remarks>
    /// <param name="gameToUpdate">The game entity to update in the database.</param>
    /// <param name="processedGame">
    /// The processed game containing values to apply to the database record.
    /// </param>
    /// <returns>True if the update was applied; otherwise, false.</returns>
    public virtual bool ProcessUpdateData(Game gameToUpdate, BaseActionGame processedGame) => false;
}