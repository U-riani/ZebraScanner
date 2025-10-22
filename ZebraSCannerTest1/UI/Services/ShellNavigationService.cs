using ZebraSCannerTest1.Core.Interfaces;

namespace ZebraSCannerTest1.UI.Services;
public class ShellNavigationService : INavigationService
{
    public async Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (parameters != null && parameters.Count > 0)
            await Shell.Current.GoToAsync(route, parameters);
        else
            await Shell.Current.GoToAsync(route);
    }
}
