using UnityEngine;

public static class SkinUnlockManager
{
    private const string PREF_KEY_PREFIX = "SkinUnlocked_";

    public static bool IsSkinUnlocked(PlayerCharacterData data)
    {
        if (data == null) return false;

        // Base/default skins are unlocked by default
        if (data.isDefaultUnlocked) return true;

        if (string.IsNullOrEmpty(data.skinId)) return true;

        return PlayerPrefs.GetInt(PREF_KEY_PREFIX + data.skinId, 0) == 1;
    }

    public static void UnlockSkin(PlayerCharacterData data)
    {
        if (data == null || string.IsNullOrEmpty(data.skinId)) return;

        PlayerPrefs.SetInt(PREF_KEY_PREFIX + data.skinId, 1);
        PlayerPrefs.Save();
        Debug.Log($"[SkinUnlockManager] Unlocked skin: {data.skinName} ({data.skinId})");
    }

    public static void ResetAllUnlocks(PlayerCharacterData[] allSkins)
    {
        if (allSkins == null) return;
        foreach (var data in allSkins)
        {
            if (data != null && !string.IsNullOrEmpty(data.skinId))
            {
                PlayerPrefs.DeleteKey(PREF_KEY_PREFIX + data.skinId);
            }
        }
        PlayerPrefs.Save();
        Debug.Log("[SkinUnlockManager] All skin unlocks reset.");
    }
}
