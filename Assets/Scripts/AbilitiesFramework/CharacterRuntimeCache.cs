using System.Collections.Generic;
using UnityEngine;

public static class CharacterRuntimeCache
{
    private static Dictionary<PlayerCharacterData, int> baseJumpCounts = new();

    public static void Register(PlayerCharacterData data)
    {
        if (data == null)
            return;

        if (!baseJumpCounts.ContainsKey(data))
        {
            baseJumpCounts[data] = data.numOfJumps;
        }
    }

    public static void ResetAll()
    {
        foreach (var pair in baseJumpCounts)
        {
            if (pair.Key != null)
                pair.Key.numOfJumps = pair.Value;
        }
    }
}