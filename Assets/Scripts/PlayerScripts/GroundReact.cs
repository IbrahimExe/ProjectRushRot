using UnityEngine;
using System.Collections;

public class GroundReact : MonoBehaviour
{
    private PlayerControllerBase controller;
    [SerializeField] private OutOfBoundsRespawn respawn;
    private ParticleSystem[] activeParticle;
    private string currentRegion;

    [Header("Ground Particles")]
    public string GrassTag = "GRASS";
    public string SandTag = "SAND";
    public string WaterTag = "WATER";
    public string DeepWaterTag = "DEEPWATER";
    public string StoneTag = "STONE";
    public string ForestTag = "FOREST";

    public ParticleSystem[] waterSplash;
    public ParticleSystem[] waterTrail;
    public ParticleSystem[] grassTrail;
    public ParticleSystem[] sandTrail;
    public ParticleSystem[] stoneTrail;
    public ParticleSystem[] forestTrail;

    [Header("Terrain Debuffs")]
    public float sandSlowAmount = 0.55f;
    public float sandJumpAmount = 0.65f;
    public float sandDebuffDuration = 1.5f;

    public float waterSlowAmount = 0.75f;
    public float waterDebuffDuration = 1.25f;

    public float deepWaterSlowAmount = 0.45f;
    public float deepWaterDebuffDuration = 2f;

    [Header("Safe Respawn")]
    [SerializeField] private float respawnHeightOffset = 1.5f;

    private Vector3 lastSafePosition;
    private Quaternion lastSafeRotation;
    private bool hasSafePosition;

    void Start() {
        
    }

    void Update()
    {
        string newRegion = GetRegionFromPlayer();
        //Debug.Log($"[GroundReact] new:'{newRegion}' current:'{currentRegion}'");
        if (newRegion != currentRegion)
        {
            currentRegion = newRegion;
            HandleRegionChange(currentRegion);
        }
    }

    private void HandleRegionChange(string region)
    {
        bool respawnSwitch = false;
        bool skipChangeParles = false;
        ParticleSystem[] newParticle = null;

        ApplyRegionDebuff(region);

        TrySaveSafePosition(region);

        switch (region)
        {
            case "DEEPWATER":
                MoveAndPlay(waterSplash);
                respawnSwitch = true;
                skipChangeParles = true;
                break;

            case "GRASS":
               // Debug.Log("Player is now on Grass. Playing grass particle.");
                 newParticle = grassTrail;
                break;

                case "SAND":
                //Debug.Log("Player is now on Sand. Playing sand particle.");
                newParticle = sandTrail;
                break;

                case "WATER":
               // Debug.Log("Player is now on Water. Playing water particle.");
                newParticle = waterTrail;
                break;

            case "STONE":
               // Debug.Log("Player is now on Stone. Playing stone particle.");
                newParticle = stoneTrail;
                break;

                case "FOREST":
                //Debug.Log("Player is now in a Forest. Playing forest particle.");
                newParticle = forestTrail;
                break;

            default:
               // Debug.Log("Player is now on an unknown surface. Remember to tag your regions");
                break;
        }

        if (!skipChangeParles)
        {
            ChangeParles(activeParticle, newParticle);
            activeParticle = newParticle;
        }

        if (respawnSwitch)
        {
            RespawnToLastSafePosition();
        }
    }

    private string GetRegionFromPlayer()
    {
        if (controller == null)
            controller = GetComponentInParent<PlayerControllerBase>();

        if (controller != null)
            return controller.lastGroundRegion;

        return string.Empty;
    }

    private void ChangeParles(ParticleSystem[] currentParticle, ParticleSystem[] newParticle)
    {
        if (currentParticle != null)
            foreach (ParticleSystem ps in currentParticle)
                if (ps != null && ps.isPlaying)
                    StartCoroutine(StopAfterDelay(ps));

        if (newParticle != null)
            foreach (ParticleSystem ps in newParticle)
                if (ps != null) ps.Play();
    }
    private void LeaveAndStop(ParticleSystem[] particles)
    {
        if (particles == null) return;
        foreach (ParticleSystem ps in particles)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void MoveAndPlay(ParticleSystem[] particles)
    {
        if (particles == null) return;
        foreach (ParticleSystem ps in particles)
        {
            if (ps == null) continue;
            ps.transform.position = transform.position;
            ps.Play();
        }
    }

    private void ApplyRegionDebuff(string region)
    {
        PlayerDebuffReceiver debuffReceiver = GetComponentInParent<PlayerDebuffReceiver>();

        if (debuffReceiver == null)
            return;

        if (IsMushroomProtected())
            return;

        switch (region)
        {
            case "SAND":
                debuffReceiver.ApplyDebuff(DebuffType.Slow, sandSlowAmount, sandDebuffDuration);
                debuffReceiver.ApplyDebuff(DebuffType.ReducedJump, sandJumpAmount, sandDebuffDuration);
                break;

            case "WATER":
                debuffReceiver.ApplyDebuff(DebuffType.Slow, waterSlowAmount, waterDebuffDuration);
                break;
        }
        Debug.Log("Terrain Debuff: " + region);
    }

    private bool IsMushroomProtected()
{
    PlayerAbilityRunner runner = GetComponentInParent<PlayerAbilityRunner>();

    if (runner == null || runner.Perks == null)
        return false;

    foreach (RuntimePerk runtime in runner.Perks.ActivePerks)
    {
        if (runtime.ability is MushroomPerk mushroom)
            return mushroom.IsProtecting;
    }

    return false;
}

    private void TrySaveSafePosition(string region)
    {
        if (
            region != "GRASS" &&
            region != "FOREST" &&
            region != "STONE"
        )
            return;

        PlayerControllerBase player = GetComponentInParent<PlayerControllerBase>();

        if (player == null)
            return;

        lastSafePosition = player.transform.position;
        lastSafeRotation = player.transform.rotation;
        hasSafePosition = true;
    }

    private void RespawnToLastSafePosition()
    {
        PlayerControllerBase player = GetComponentInParent<PlayerControllerBase>();

        if (player == null)
            return;

        if (!hasSafePosition)
        {
            if (respawn != null)
                respawn.RespawnPlayer();

            return;
        }

        Rigidbody rb = player.RB;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.transform.position = lastSafePosition + Vector3.up * respawnHeightOffset;
        player.transform.rotation = lastSafeRotation;
    }

    private IEnumerator StopAfterDelay(ParticleSystem ps)
    {
        // Stop emitting new particles but let existing ones finish
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Wait until all particles have died
        yield return new WaitUntil(() => ps == null || !ps.IsAlive());

        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
