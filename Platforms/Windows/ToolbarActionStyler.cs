#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Controls;
using ProjectTimer.Services;

namespace ProjectTimer.Platforms.Windows;

public sealed class ToolbarActionStyler
{
    private readonly List<WeakReference<AppBarButton>> _actions = [];

    public ToolbarActionStyler(ThemeService themeService)
    {
        themeService.ThemeChanged += (_, _) => Refresh();
    }

    public void Apply(AppBarButton action)
    {
        if (!_actions.Any(reference => reference.TryGetTarget(out var existing) && existing == action))
        {
            _actions.Add(new WeakReference<AppBarButton>(action));
        }

        ApplyTheme(action);
    }

    private void Refresh()
    {
        for (var index = _actions.Count - 1; index >= 0; index--)
        {
            if (_actions[index].TryGetTarget(out var action))
            {
                ApplyTheme(action);
            }
            else
            {
                _actions.RemoveAt(index);
            }
        }
    }

    private static void ApplyTheme(AppBarButton action)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources["Primary"] is not Color primary ||
            Microsoft.Maui.Controls.Application.Current.Resources["PrimaryContainer"] is not Color primaryContainer ||
            Microsoft.Maui.Controls.Application.Current.Resources["OnPrimaryContainer"] is not Color onPrimaryContainer)
        {
            return;
        }

        action.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(primaryContainer.ToWindowsColor());
        action.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(primary.ToWindowsColor());
        action.BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5);
        action.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(onPrimaryContainer.ToWindowsColor());
        action.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16);
        action.Padding = new Microsoft.UI.Xaml.Thickness(14, 8, 16, 8);
        action.Margin = new Microsoft.UI.Xaml.Thickness(4, 0, 0, 0);
    }
}
#endif
