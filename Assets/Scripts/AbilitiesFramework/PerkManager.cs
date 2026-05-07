using System.Collections.Generic;
using UnityEngine;

public class PerkManager
{
    private PlayerAbilityContext ctx;

    private readonly Dictionary<string, RuntimePerk> active = new();
    private readonly Dictionary<string, PerkRuntimeState> states = new();

    public IEnumerable<RuntimePerk> ActivePerks => active.Values;

    public void Initialize(PlayerAbilityContext context)
    {
        ctx = context;
    }

    public PerkRuntimeState GetState(string abilityId)
    {
        if (!states.TryGetValue(abilityId, out PerkRuntimeState state))
        {
            state = new PerkRuntimeState();
            states.Add(abilityId, state);
        }

        return state;
    }

    public void Apply(AbilityBase ability)
    {
        if (ability == null || ctx == null)
            return;

        if (active.ContainsKey(ability.abilityId))
        {
            Upgrade(ability.abilityId);
            return;
        }

        RuntimePerk runtime = new RuntimePerk(ability, 1);
        active.Add(ability.abilityId, runtime);

        ability.OnApply(ctx, 1);
        RecalculateStats();
    }

    public void Remove(string abilityId)
    {
        if (!active.TryGetValue(abilityId, out RuntimePerk runtime))
            return;

        runtime.ability.OnRemove(ctx);
        active.Remove(abilityId);
        states.Remove(abilityId);

        RecalculateStats();
    }

    public int GetLevel(string abilityId)
    {
        return active.TryGetValue(abilityId, out RuntimePerk runtime) ? runtime.level : 0;
    }

    public bool HasPerk(string abilityId)
    {
        return active.ContainsKey(abilityId);
    }

    public void Tick(float deltaTime)
    {
        foreach (RuntimePerk runtime in active.Values)
            runtime.ability.Tick(ctx, runtime.level, deltaTime);
    }

    public void FixedTick(float fixedDeltaTime)
    {
        foreach (RuntimePerk runtime in active.Values)
            runtime.ability.FixedTick(ctx, runtime.level, fixedDeltaTime);
    }

    public bool TryUse(string abilityId)
    {
        Debug.Log("Trying ability: " + abilityId);

        if (!active.TryGetValue(abilityId, out RuntimePerk runtime))
        {
            Debug.LogWarning("Ability not active/found: " + abilityId);
            return false;
        }

        Debug.Log("Found ability: " + runtime.ability.displayName + " Level: " + runtime.level);
        return runtime.ability.TryUse(ctx, runtime.level);
    }

    private void Upgrade(string abilityId)
    {
        RuntimePerk runtime = active[abilityId];

        int oldLevel = runtime.level;
        int newLevel = Mathf.Min(oldLevel + 1, runtime.ability.maxLevel);

        if (newLevel == oldLevel)
            return;

        runtime.level = newLevel;
        runtime.ability.OnUpgrade(ctx, oldLevel, newLevel);

        RecalculateStats();
    }

    private void RecalculateStats()
    {
        ctx.player.SetBaseStats();

        foreach (RuntimePerk runtime in active.Values)
        {
            foreach (StatModifier mod in runtime.ability.GetStatModifiers(runtime.level))
                ApplyModifier(mod);
        }
    }

    private void ApplyModifier(StatModifier mod)
    {
        switch (mod.statKey)
        {
            case "maxSpeed":
                ctx.player.addMaxSpeed(mod.value);
                break;

            case "acceleration":
                ctx.player.addAcceleration(mod.value);
                break;

            case "jumpForce":
                ctx.player.addJumpForce(mod.value);
                break;

            case "wallRunDuration":
                ctx.player.addWallRunDuration(mod.value);
                break;

            case "wallRunSpeed":
                ctx.player.addWallRunSpeed(mod.value);
                break;

            case "wallJumpUpImpulse":
                ctx.player.addWallJumpUpImpulse(mod.value);
                break;

            case "wallJumpAwayImpulse":
                ctx.player.addWallJumpAwayImpulse(mod.value);
                break;

            case "dashKillWindow":
                ctx.player.addDashKillWindow(mod.value);
                break;

            case "dashKillCount":
                ctx.player.addDashKillCount(Mathf.RoundToInt(mod.value));
                break;

            case "bonusAirJumps":
                ctx.player.bonusAirJumps += Mathf.RoundToInt(mod.value);
                break;
        }
    }
}

public class RuntimePerk
{
    public AbilityBase ability;
    public int level;

    public RuntimePerk(AbilityBase ability, int level)
    {
        this.ability = ability;
        this.level = level;
    }
}