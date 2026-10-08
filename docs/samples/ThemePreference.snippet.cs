// После входа, когда логин уже известен.
var login = MainViewModel.Current != null ? MainViewModel.Current.Login : string.Empty;
var themeName = UserSettingsStorage.Load(
    UserSettingsStorage.ThemeSection,
    UserSettingsStorage.HashLogin(login),
    "Default");
UiTheme.Apply(themeName);

// ThemeMenuItem_OnClick в шапке.
private void ThemeMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    var item = sender as MenuItem;
    var themeName = item != null ? item.Tag as string : null;
    if (string.IsNullOrEmpty(themeName))
        return;

    var login = MainViewModel.Current != null ? MainViewModel.Current.Login : string.Empty;
    UserSettingsStorage.Save(
        UserSettingsStorage.ThemeSection,
        UserSettingsStorage.HashLogin(login),
        themeName);
    UiTheme.Apply(themeName);
}
