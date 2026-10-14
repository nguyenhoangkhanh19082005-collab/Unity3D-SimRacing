using System;
using System.IO;
using UnityEngine;

namespace SimRacing
{
    public static class LocalStore
    {
        public static string DirectoryPath => Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--verify")
            ? Path.GetFullPath(Path.Combine(Application.dataPath,"..","Verification","Saves"))
            : Path.Combine(Application.persistentDataPath, "UnitySaveV1");
        public static T Load<T>(string file, T fallback, Func<T, bool> valid) where T : class
        {
            foreach (string suffix in new[] { "", ".backup" }) {
                try {
                    string path = Path.Combine(DirectoryPath, file + suffix);
                    if (!File.Exists(path)) continue;
                    T data = JsonUtility.FromJson<T>(File.ReadAllText(path));
                    if (data != null && valid(data)) return data;
                } catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { Debug.LogWarning(e.Message); }
            }
            return fallback;
        }
        public static bool Save<T>(string file, T value)
        {
            try {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, file), temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(value, true));
                if (File.Exists(path)) File.Replace(temp, path, path + ".backup");
                else File.Move(temp, path);
                return true;
            } catch (Exception e) { Debug.LogError("Không lưu được dữ liệu: " + e.Message); return false; }
        }
    }
}
