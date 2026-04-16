using System;

[Serializable]
public struct StatModifier
{
    public enum ModType { Flat, Multiplicative }

    public string statKey;
    public float value;
    public ModType modType;

    public StatModifier(string key, float val, ModType type = ModType.Flat)
    {
        statKey = key;
        value = val;
        modType = type;
    }
}