using System.Collections.Generic;
using UnityEngine;

// The player's items. Kept between scenes and saved between sessions.
// Items of the same kind stack (up to Max Stack in ItemSettings); scrolls are kept one by one with their own text.
public static class Inventory
{
    [System.Serializable]
    public class Entry
    {
        public ItemType type;
        public int count = 1;
        public string title; // scrolls only
        public string text;  // scrolls only
    }

    [System.Serializable]
    class SaveData { public List<Entry> entries = new List<Entry>(); }

    const string SaveKey = "inventory";
    static List<Entry> entries;

    public static event System.Action Changed;

    public static IReadOnlyList<Entry> Entries { get { Load(); return entries; } }

    public static int Count(ItemType type)
    {
        Load();
        int n = 0;
        foreach (Entry e in entries) if (e.type == type) n += e.count;
        return n;
    }

    // Returns false if it could not be added (that stack is full)
    public static bool Add(ItemType type, int amount = 1, string title = null, string text = null)
    {
        Load();
        if (type == ItemType.Scroll)
        {
            for (int i = 0; i < amount; i++) entries.Add(new Entry { type = type, title = title, text = text });
        }
        else
        {
            Entry stack = entries.Find(e => e.type == type);
            int max = ItemSettings.Current.maxStack;
            if (stack == null) entries.Add(stack = new Entry { type = type, count = 0 });
            if (stack.count >= max) return false;
            stack.count = Mathf.Min(stack.count + amount, max);
        }
        Save();
        return true;
    }

    // Takes one item out of the entry at this index
    public static void RemoveOne(int index)
    {
        Load();
        if (index < 0 || index >= entries.Count) return;
        entries[index].count--;
        if (entries[index].count <= 0) entries.RemoveAt(index);
        Save();
    }

    public static bool RemoveOne(ItemType type)
    {
        Load();
        int i = entries.FindIndex(e => e.type == type);
        if (i < 0) return false;
        RemoveOne(i);
        return true;
    }

    public static void Clear()
    {
        entries = new List<Entry>();
        Save();
    }

    static void Load()
    {
        if (entries != null) return;
        string json = PlayerPrefs.GetString(SaveKey, "");
        entries = string.IsNullOrEmpty(json) ? new List<Entry>() : (JsonUtility.FromJson<SaveData>(json)?.entries ?? new List<Entry>());
    }

    static void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData { entries = entries }));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Editor testing tools change the saved data directly; this makes the game read it again
    public static void Reload()
    {
        entries = null;
        Load();
        Changed?.Invoke();
    }
}
