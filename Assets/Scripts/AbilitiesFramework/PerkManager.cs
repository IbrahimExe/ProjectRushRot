using System.Collections.Generic;
using UnityEngine;

public class PerkManager
{
    [SerializeField] private PlayerControllerBase player;

    private readonly Dictionary<string, (AbilityBase ability, int level)> _active = new();

    public void Apply(AbilityBase ability)
    {
        if (_active.ContainsKey(ability.abilityId))
        {
            Upgrade(ability.abilityId);
            return;
        }
        _active[ability.abilityId] = (ability, 1);
        ability.OnApply(player, 1);
        RecalculateStats();
    }

    public void Remove(string abilityId)
    {
        if (!_active.TryGetValue(abilityId, out var entry)) return;
        entry.ability.OnRemove(player);
        _active.Remove(abilityId);
        RecalculateStats();
    }

    public int GetLevel(string abilityId) =>
        _active.TryGetValue(abilityId, out var entry) ? entry.level : 0;

    public bool HasPerk(string abilityId) => _active.ContainsKey(abilityId);

    private void Upgrade(string abilityId)
    {
        var entry = _active[abilityId];
        int oldLevel = entry.level;
        int newLevel = Mathf.Min(oldLevel + 1, entry.ability.maxLevel);
        _active[abilityId] = (entry.ability, newLevel);
        entry.ability.OnUpgrade(player, oldLevel, newLevel);
        RecalculateStats();
    }

    private void RecalculateStats()
    {
        //player.ResetStats();
        //foreach (var (ability, level) in _active.Values)
        //    foreach (var mod in ability.GetStatModifiers(level))
        //        player.ApplyStatModifier(mod);
    }
}