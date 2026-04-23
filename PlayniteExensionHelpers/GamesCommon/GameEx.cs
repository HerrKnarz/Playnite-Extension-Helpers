using Playnite;

namespace PlayniteExtensionHelpers.GamesCommon;

public class GameEx(Game game)
{
    public Game Game { get; set; } = game;

    public bool NeedsToBeUpdated { get; set; } = false;

    public string? Platforms { get; set; }

    public string RealSortingName => Game.SortingName.IsNullOrEmpty() ? Game.Name : Game.SortingName;
}