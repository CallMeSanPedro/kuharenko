using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace Store.Views.Localization
{
    /// <summary>
    /// Строки интерфейса. Нейтральный язык — русский (Strings.resx).
    /// Английский — Strings.en.resx. Индексатор возвращает null, если ключа нет,
    /// чтобы в гриде остался ShortName (TargetNullValue / FallbackValue).
    /// Привязка: {Binding [Menu_Warehouse], Source={x:Static loc:Translation.Instance}}.
    /// </summary>
    public sealed class Translation : INotifyPropertyChanged
    {
        public static readonly Translation Instance = new Translation();

        private readonly ResourceManager _resources =
            new ResourceManager("Store.Views.Localization.Strings", typeof(Translation).Assembly);

        private CultureInfo _culture = CultureInfo.GetCultureInfo("ru");

        private Translation()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CultureInfo Culture => _culture;

        public string this[string key] => Lookup(key);

        public string Get(string key, string fallback)
        {
            var value = Lookup(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        public void SetCulture(string name)
        {
            var culture = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(name) ? "ru" : name);
            var changed = !Equals(_culture, culture);
            _culture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            if (!changed)
                return;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }

        private string Lookup(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            return _resources.GetString(key, _culture);
        }
    }
}
