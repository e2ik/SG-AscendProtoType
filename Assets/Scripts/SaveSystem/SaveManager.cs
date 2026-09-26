using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class CharacterData
{
    public string characterName;
    public int appearanceId;
    public Color hairColor;
    public Color skinColor;
    public Color eyeColor;
    public Color clothesTopColor;
    public Color clothesBottomColor;
    public Color shoesColor;
}

[Serializable]
public class FlagEntry
{
    public string key;
    public bool value;
}

[Serializable]
public class SaveData
{
    public string saveId;
    public string saveName;
    public string lastPlayedUtc;
    public CharacterData character;
    public string currentSceneName;
    public Vector3 playerPosition;
    public PlayerStats stats = new PlayerStats();
    public List<FlagEntry> flags = new List<FlagEntry>();

    public bool GetFlag(string key)
    {
        var entry = flags.Find(f => f.key == key);
        return entry != null && entry.value;
    }

    public void SetFlag(string key, bool value)
    {
        var entry = flags.Find(f => f.key == key);
        if (entry != null) entry.value = value;
        else flags.Add(new FlagEntry { key = key, value = value });
    }
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public SaveData CurrentSave { get; private set; }

    private string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!Directory.Exists(SaveFolder))
            Directory.CreateDirectory(SaveFolder);
    }

    private string PathFor(string saveId) => Path.Combine(SaveFolder, $"{saveId}.json");

    public SaveData CreateNewSave(CharacterData character, string startingScene)
    {
        var save = new SaveData
        {
            saveId = Guid.NewGuid().ToString(),
            saveName = string.IsNullOrEmpty(character.characterName) ? "New Save" : character.characterName,
            lastPlayedUtc = DateTime.UtcNow.ToString("o"),
            character = character,
            currentSceneName = startingScene,
            playerPosition = Vector3.zero
        };

        CurrentSave = save;
        WriteToDisk(save);
        return save;
    }

    public bool LoadSave(string saveId)
    {
        string path = PathFor(saveId);
        if (!File.Exists(path))
        {
            Debug.LogError($"Save file not found: {path}");
            return false;
        }

        var save = ReadSave(path);
        if (save == null) return false;

        CurrentSave = save;
        return true;
    }

    public void SaveCurrent()
    {
        if (CurrentSave == null)
        {
            Debug.LogWarning("No active save to write.");
            return;
        }

        CurrentSave.lastPlayedUtc = DateTime.UtcNow.ToString("o");
        WriteToDisk(CurrentSave);
    }

    private void WriteToDisk(SaveData save)
    {
        string json = JsonUtility.ToJson(save, prettyPrint: true);
        File.WriteAllText(PathFor(save.saveId), json);
    }

    public void DeleteSave(string saveId)
    {
        string path = PathFor(saveId);
        if (File.Exists(path))
            File.Delete(path);

        if (CurrentSave != null && CurrentSave.saveId == saveId)
            CurrentSave = null;
    }

    public void DeleteAllSaves()
    {
        if (Directory.Exists(SaveFolder))
        {
            foreach (var file in Directory.GetFiles(SaveFolder, "*.json"))
                File.Delete(file);
        }

        CurrentSave = null;
    }

    public List<SaveData> GetAllSaves()
    {
        var result = new List<SaveData>();
        if (!Directory.Exists(SaveFolder)) return result;

        foreach (var file in Directory.GetFiles(SaveFolder, "*.json"))
        {
            var save = ReadSave(file);
            if (save != null)
                result.Add(save);
        }

        result.Sort((a, b) => string.CompareOrdinal(b.lastPlayedUtc, a.lastPlayedUtc));
        return result;
    }

    private static SaveData ReadSave(string path)
    {
        try
        {
            var save = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (save == null || string.IsNullOrEmpty(save.saveId))
            {
                Debug.LogError($"Save file is empty or invalid: {path}");
                return null;
            }

            save.stats ??= new PlayerStats();
            save.flags ??= new List<FlagEntry>();
            return save;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save {path}: {e.Message}");
            return null;
        }
    }
}