using Playnite;

namespace PlayniteExtensionHelpers.GamesCommon;

public class BaseActionArgs(IPlayniteApi api, List<GameEx> games, string pluginName)
{
    public IPlayniteApi Api { get; } = api;
    public virtual bool DebugMode { get; set; } = false;
    public virtual bool GamesNeedUpdate { get; set; } = true;
    public virtual bool IsBulkAction { get; set; } = games.Count > 1;
    public string PluginName { get; set; } = pluginName;
    public virtual bool ShowDialogs { get; set; } = true;
}