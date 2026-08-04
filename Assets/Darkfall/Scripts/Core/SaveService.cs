using UnityEngine;

namespace Darkfall.Core
{
    public static class SaveService
    {
        private const string Key = "darkfall.save.v1";

        public static SaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new SaveData();
            try
            {
                return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key)) ?? new SaveData();
            }
            catch
            {
                return new SaveData();
            }
        }

        public static void Save(SaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
