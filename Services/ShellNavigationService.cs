namespace ProjectTimer.Services;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        return parameters is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, parameters);
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
