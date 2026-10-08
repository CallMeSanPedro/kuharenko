// После входа, когда логин уже известен.
var login = MainViewModel.Current != null ? MainViewModel.Current.Login : string.Empty;
UiTheme.Apply(PinnedStoresStorage.LoadTheme(login));

// ThemeMenuItem_OnClick в шапке.
private void ThemeMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    var item = sender as MenuItem;
    var themeName = item != null ? item.Tag as string : null;
    if (string.IsNullOrEmpty(themeName))
        return;

    var login = MainViewModel.Current != null ? MainViewModel.Current.Login : string.Empty;
    PinnedStoresStorage.SaveTheme(login, themeName);
    UiTheme.Apply(themeName);
}
