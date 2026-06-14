using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace PlayniteExtensionHelpers.UICommon.Controls;

public class SettingsSection : SettingsBaseControl
{
    public static readonly DependencyProperty HelpCommandProperty =
        DependencyProperty.Register(
            "HelpCommand",
            typeof(RelayCommand),
            typeof(SettingsSection));

    public RelayCommand HelpCommand
    {
        get => (RelayCommand)GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }
}