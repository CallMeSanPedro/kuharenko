using System;
using System.ComponentModel;
using System.IO;
using Store.Views.Localization;

internal static class Program
{
    private static int Main()
    {
        var failed = 0;
        failed += Expect("ru warehouse", "Склад", "ru", "Menu_Warehouse");
        failed += Expect("en warehouse", "Warehouse", "en", "Menu_Warehouse");
        failed += Expect("en serial", "Serial number", "en", "VerifyIdentifierChange_SerialNumber");
        failed += Expect("ru serial", "Серийный номер", "ru", "TechReserv_SerialNumber");

        Translation.Instance.SetCulture("en");
        var missing = Translation.Instance.Get("No_Such_Key", "Серийный номер");
        if (missing != "Серийный номер")
        {
            Console.Error.WriteLine("fallback got " + missing);
            failed++;
        }

        if (Translation.Instance["No_Such_Key"] != null)
        {
            Console.Error.WriteLine("missing key must be null");
            failed++;
        }

        var raised = false;
        PropertyChangedEventHandler handler = (sender, args) =>
        {
            if (args.PropertyName == "Item[]")
                raised = true;
        };
        Translation.Instance.PropertyChanged += handler;
        Translation.Instance.SetCulture("ru");
        Translation.Instance.PropertyChanged -= handler;
        if (!raised)
        {
            Console.Error.WriteLine("Item[] was not raised");
            failed++;
        }

        var path = Path.Combine(Path.GetTempPath(), "store-language-" + Guid.NewGuid().ToString("N") + ".json");
        LanguagePreference.FilePath = path;
        LanguagePreference.Apply("ivan", "en");
        Translation.Instance.SetCulture("ru");
        LanguagePreference.Apply("ivan");
        if (Translation.Instance.Culture.Name != "en")
        {
            Console.Error.WriteLine("saved culture " + Translation.Instance.Culture.Name);
            failed++;
        }

        LanguagePreference.Apply("petr");
        if (Translation.Instance.Culture.Name != "ru")
        {
            Console.Error.WriteLine("other login " + Translation.Instance.Culture.Name);
            failed++;
        }

        if (File.Exists(path))
            File.Delete(path);

        return failed == 0 ? 0 : 1;
    }

    private static int Expect(string label, string expected, string culture, string key)
    {
        Translation.Instance.SetCulture(culture);
        var actual = Translation.Instance[key];
        if (actual == expected)
            return 0;

        Console.Error.WriteLine(label + " got " + actual);
        return 1;
    }
}
