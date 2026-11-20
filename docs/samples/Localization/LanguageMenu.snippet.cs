// MainView.xaml.cs. Пункт «Язык» в гамбургере, Click="LanguageMenuItem_OnClick".
// Тот же login, что у темы. Вызов после входа, до Show главного окна:
//
//     LanguagePreference.Apply(login);
//
// Повторный вызов при смене языка пишет language.json и обновляет все привязки к Translation.Instance.

using System.Windows;
using System.Windows.Controls;
using Store.Views.Localization;

private string _login;

private void LanguageMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    var culture = (sender as MenuItem)?.Tag as string;
    if (string.IsNullOrEmpty(culture))
        return;

    LanguagePreference.Apply(_login, culture);
}
