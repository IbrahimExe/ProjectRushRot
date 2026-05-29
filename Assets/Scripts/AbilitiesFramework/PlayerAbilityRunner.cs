using UnityEngine;

public class PlayerAbilityRunner : MonoBehaviour
{
    public PlayerControllerBase player;
    public LayerMask abilityMask;

    [Header("Starting Perks")]
    public AbilityBase[] startingPerks;

    private PlayerAbilityContext ctx;
    private PerkManager perkManager;
    private bool initialized;

    public PerkManager Perks => perkManager;

    private void Awake()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    private void OnEnable()
    {
        //Debug.Log("ABILITY RUNNER ENABLED");

        if (!initialized)
            SystemLoader.CallOnComplete(Initialize);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        if (player == null)
            player = GetComponent<PlayerControllerBase>();

        if (player == null)
        {
            Debug.LogError("PlayerAbilityRunner: Missing PlayerControllerBase.");
            return;
        }

        perkManager = new PerkManager();
        ctx = new PlayerAbilityContext(player, abilityMask, perkManager);
        perkManager.Initialize(ctx);

        foreach (AbilityBase perk in startingPerks)
        {
            if (perk != null)
                perkManager.Apply(perk);
        }

        initialized = true;
        //Debug.Log("PlayerAbilityRunner INITIALIZED");
    }

    private void Update()
    {
        if (!initialized || perkManager == null)
            return;

        perkManager.Tick(Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
           // Debug.Log("Pressed 1: trying missile");
            bool used = perkManager.TryUse("missile");
           // Debug.Log("Missile used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
           // Debug.Log("Pressed 2: trying shield");
            bool used = perkManager.TryUse("shield");
           // Debug.Log("Shield used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
           // Debug.Log("Pressed 3: trying ground pound");
            bool used = perkManager.TryUse("ground_pound");
           // Debug.Log("Ground pound used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            //Debug.Log("Pressed 4: trying snowball");
            bool used = perkManager.TryUse("snowball");
           // Debug.Log("Snowball used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
           // Debug.Log("Pressed 5: trying beanstalk");
            bool used = perkManager.TryUse("beanstalk");
            //Debug.Log("Beanstalk used: " + used);
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || perkManager == null)
            return;

        perkManager.FixedTick(Time.fixedDeltaTime);
    }

    public void AddPerk(AbilityBase perk)
    {
        if (!initialized || perkManager == null)
            Initialize();

        if (perkManager != null && perk != null)
            perkManager.Apply(perk);
    }

    public void RecalculateStats()
    {
        if (perkManager != null)
            perkManager.RecalculateStats();
    }

    public void ClearAllPerks()
    {
        if (perkManager != null)
            perkManager.ClearAllPerks();
    }
}