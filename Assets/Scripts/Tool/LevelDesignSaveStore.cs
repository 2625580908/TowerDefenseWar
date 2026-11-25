using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>One file per design; the old single save is imported without being removed.</summary>
public sealed class LevelDesignSaveStore
{
    public sealed class DuplicateTerrainException : InvalidOperationException
    {
        public LevelDesignSaveData ExistingSave { get; }
        public DuplicateTerrainException(LevelDesignSaveData existingSave)
            : base("相同地形已保存在关卡库中，请修改地图后再保存。")
        {
            ExistingSave = existingSave;
        }
    }

    private readonly string legacyPath;
    public string DirectoryPath { get; }

    public LevelDesignSaveStore(string latestSavePath)
    {
        legacyPath = latestSavePath;
        DirectoryPath = Path.Combine(Path.GetDirectoryName(latestSavePath),
            Path.GetFileNameWithoutExtension(latestSavePath) + "-levels");
    }

    public LevelDesignSaveData SaveNew(LevelDesignSaveData source, IList<string> legacyPalette = null)
    {
        if (source == null || !source.IsValid)
            throw new ArgumentException("关卡存档数据不完整。");
        foreach (var existing in GetAll())
            if (HasSameTerrain(source, existing, legacyPalette))
                throw new DuplicateTerrainException(existing);
        var save = JsonUtility.FromJson<LevelDesignSaveData>(JsonUtility.ToJson(source));
        save.id = Guid.NewGuid().ToString("N");
        save.savedUtcTicks = DateTime.UtcNow.Ticks;
        save.displayName = "设计关卡 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        WriteNewFile(GetPath(save.id), JsonUtility.ToJson(save, true));
        // The archive remains authoritative if the compatibility snapshot cannot be updated.
        try
        {
            WriteSnapshot(legacyPath, JsonUtility.ToJson(save, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"关卡已保存，最近存档快照更新失败：{exception.Message}");
        }
        return save;
    }

    public List<LevelDesignSaveData> GetAll()
    {
        ImportLegacySave();
        var result = new List<LevelDesignSaveData>();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            var save = ReadFile(path);
            // Require the embedded identity to match the file used for opening it.
            if (save != null && Path.GetFileNameWithoutExtension(path) == save.id)
                result.Add(save);
        }
        result.Sort((left, right) =>
        {
            var order = right.savedUtcTicks.CompareTo(left.savedUtcTicks);
            return order != 0 ? order : string.CompareOrdinal(right.id, left.id);
        });
        return result;
    }

    public LevelDesignSaveData Update(string id, LevelDesignSaveData source, IList<string> legacyPalette = null)
    {
        if (source == null || !source.IsValid)
            throw new ArgumentException("关卡存档数据不完整。");
        var path = GetPath(id);
        var saves = GetAll();
        var original = saves.Find(save => save.id == id);
        if (original == null)
            throw new FileNotFoundException("原关卡已不存在，无法保存编辑结果。");
        foreach (var existing in saves)
            if (existing.id != id && HasSameTerrain(source, existing, legacyPalette))
                throw new DuplicateTerrainException(existing);
        var updated = JsonUtility.FromJson<LevelDesignSaveData>(JsonUtility.ToJson(source));
        updated.id = original.id;
        updated.displayName = original.displayName;
        updated.savedUtcTicks = DateTime.UtcNow.Ticks;
        // Replace atomically so a failed write leaves the original design intact.
        WriteSnapshot(path, JsonUtility.ToJson(updated, true));
        try
        {
            WriteSnapshot(legacyPath, JsonUtility.ToJson(updated, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"关卡已更新，最近存档快照更新失败：{exception.Message}");
        }
        return updated;
    }

    public LevelDesignSaveData LoadLatest()
    {
        var saves = GetAll();
        return saves.Count > 0 ? saves[0] : null;
    }

    public LevelDesignSaveData Load(string id)
    {
        return ReadFile(GetPath(id));
    }

    public bool Delete(string id)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
            return false;
        // Keep the old import from resurrecting a deliberately deleted legacy design.
        if (id == "legacy")
            File.WriteAllText(Path.Combine(DirectoryPath, ".legacy-deleted"), string.Empty);
        File.Delete(path);
        try
        {
            var snapshot = ReadFile(legacyPath);
            if (snapshot != null && (snapshot.id == id
                || (id == "legacy" && string.IsNullOrEmpty(snapshot.id))))
            {
                var latest = LoadLatest();
                if (latest == null)
                    File.Delete(legacyPath);
                else
                    WriteSnapshot(legacyPath, JsonUtility.ToJson(latest, true));
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"关卡已删除，最近存档快照更新失败：{exception.Message}");
        }
        return true;
    }

    private static bool HasSameTerrain(LevelDesignSaveData left, LevelDesignSaveData right,
        IList<string> legacyPalette)
    {
        if (left.width != right.width || left.height != right.height)
            return false;
        for (var index = 0; index < left.brushIndices.Count; index++)
            if (TileIdentity(left, left.brushIndices[index], legacyPalette)
                != TileIdentity(right, right.brushIndices[index], legacyPalette))
                return false;
        return true;
    }

    private static string TileIdentity(LevelDesignSaveData data, int index, IList<string> legacyPalette)
    {
        if (index < 0)
            return "empty";
        var palette = data.version >= 2 ? data.brushSpriteNames : legacyPalette;
        return palette != null && index < palette.Count && !string.IsNullOrEmpty(palette[index])
            ? "sprite:" + palette[index] : $"index:v{data.version}:{index}";
    }

    private void ImportLegacySave()
    {
        Directory.CreateDirectory(DirectoryPath);
        var importedPath = GetPath("legacy");
        if (File.Exists(importedPath) || File.Exists(Path.Combine(DirectoryPath, ".legacy-deleted")))
            return;
        var save = ReadFile(legacyPath);
        if (save == null)
            return;
        // A version with an ID has already been archived. Never import that snapshot twice.
        if (!string.IsNullOrEmpty(save.id))
            return;
        save.id = "legacy";
        save.displayName = "上次保存的设计关卡";
        save.savedUtcTicks = File.GetLastWriteTimeUtc(legacyPath).Ticks;
        WriteNewFile(importedPath, JsonUtility.ToJson(save, true));
    }

    private string GetPath(string id)
    {
        if (id != "legacy" && (!Guid.TryParseExact(id, "N", out _)))
            throw new ArgumentException("关卡编号无效。");
        return Path.Combine(DirectoryPath, id + ".json");
    }

    private static LevelDesignSaveData ReadFile(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var save = JsonUtility.FromJson<LevelDesignSaveData>(File.ReadAllText(path));
            if (save != null && save.IsValid)
                return save;
            Debug.LogWarning($"忽略无效设计关卡存档：{path}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"设计关卡读取失败：{path}，{exception.Message}");
        }
        return null;
    }

    private static void WriteNewFile(string path, string json)
    {
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void WriteSnapshot(string path, string json)
    {
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
