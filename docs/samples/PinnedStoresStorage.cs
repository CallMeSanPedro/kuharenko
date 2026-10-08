using System.Collections.Generic;

namespace Store.ViewModels.Tree
{
    public static class PinnedStoresStorage
    {
        public static string HashLogin(string login)
        {
            return UserSettingsStorage.HashLogin(login);
        }

        public static List<decimal> Load(string key)
        {
            return UserSettingsStorage.Load(UserSettingsStorage.PinnedSection, key, new List<decimal>())
                   ?? new List<decimal>();
        }

        public static void Save(string key, List<decimal> unids)
        {
            UserSettingsStorage.Save(UserSettingsStorage.PinnedSection, key, unids ?? new List<decimal>());
        }
    }
}
