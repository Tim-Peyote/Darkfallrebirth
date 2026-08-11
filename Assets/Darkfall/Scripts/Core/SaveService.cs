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

        /// <summary>
        /// Captures the exact persisted representation, including the distinction between an
        /// absent save and a default save. Release smoke tests use this to remain non-destructive.
        /// </summary>
        public static string CaptureRaw() => PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;

        public static void RestoreRaw(string snapshot)
        {
            if (snapshot == null) PlayerPrefs.DeleteKey(Key);
            else PlayerPrefs.SetString(Key, snapshot);
            PlayerPrefs.Save();
        }
    }
}
