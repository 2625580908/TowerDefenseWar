using System;
using System.Collections.Generic;

[Serializable]
public sealed class LevelDesignSaveData
{
    public int version = 1;
    public string id;
    public string displayName;
    public long savedUtcTicks;
    public int width;
    public int height;
    public List<int> brushIndices = new();
    public List<string> brushSpriteNames = new();

    public bool IsValid => (version == 1 || version == 2)
        && width > 0 && width <= 100 && height > 0 && height <= 100
        && brushIndices != null && (long)width * height == brushIndices.Count;
}
