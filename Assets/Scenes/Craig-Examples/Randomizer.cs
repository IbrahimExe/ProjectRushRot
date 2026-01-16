using System;
using UnityEngine;

public class Randomizer: MonoBehaviour
{
    [Header("Seed Configuration")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool useRandomSeedOnStart = false;

    [Header("Rarity Thresholds (0-99)")]
    [Range(0, 100)] public int uncommonThreshold = 60; // Top 40% are Uncommon or better
    [Range(0, 100)] public int rareThreshold = 85;     // Top 15% are Rare or better
    [Range(0, 100)] public int epicThreshold = 95;     // Top 5% are Epic

    private System.Random _random;

    void Start()
    {
        InitializeRandom();
        GenerateBatch(10);
    }

    private void InitializeRandom()
    {
        if (useRandomSeedOnStart)
        {
            seed = Environment.TickCount;
        }

        // Initialize with the serialized seed
        _random = new System.Random(seed);
        Debug.Log($"<color=green>Seed Initialized:</color> {seed}");
    }

    public void GenerateBatch(int count)
    {
        for (int i = 0; i < count; i++)
        {
            DetermineRarity();
        }
    }

    private void DetermineRarity()
    {
        // Get a large random non-negative integer
        int rawRandom = _random.Next();

        // Use modulo to constrain the result to a 0-99 range
        int roll = rawRandom % 100;

        string rarity;

        // Weighted logic based on thresholds
        if (roll >= epicThreshold)
            rarity = "<color=purple>EPIC</color>";
        else if (roll >= rareThreshold)
            rarity = "<color=blue>RARE</color>";
        else if (roll >= uncommonThreshold)
            rarity = "<color=green>UNCOMMON</color>";
        else
            rarity = "Common";

        Debug.Log($"Roll: {roll} | Result: {rarity}");
    }
}
