using UnityEngine;

public class PlayerAbilityRunner : MonoBehaviour
{
    public PlayerControllerBase player;
    public LayerMask abilityMask;

    [Header("Starting Perks")]
    public AbilityBase[] startingPerks;

    private PlayerAbilityContext ctx;
    private PerkManager perkManager;

    public PerkManager Perks => perkManager;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerControllerBase>();

        perkManager = new PerkManager();
        ctx = new PlayerAbilityContext(player, abilityMask, perkManager);
        perkManager.Initialize(ctx);

        foreach (AbilityBase perk in startingPerks)
            perkManager.Apply(perk);
    }

    private void OnEnable()
    {
        Debug.LogError("ABILITY RUNNER ENABLED");
    }

    private void Start()
    {
        Debug.Log("PlayerAbilityRunner STARTED");
    }

    private void Update()
    {
        perkManager.Tick(Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("Pressed 1: trying missile");
            bool used = perkManager.TryUse("missile");
            Debug.Log("Missile used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("Pressed 2: trying shield");
            bool used = perkManager.TryUse("shield");
            Debug.Log("Shield used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("Pressed 3: trying ground pound");
            bool used = perkManager.TryUse("ground_pound");
            Debug.Log("Ground pound used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Debug.Log("Pressed 4: trying snowball");
            bool used = perkManager.TryUse("snowball");
            Debug.Log("Snowball used: " + used);
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            Debug.Log("Pressed 5: trying beanstalk");
            bool used = perkManager.TryUse("beanstalk");
            Debug.Log("Beanstalk used: " + used);
        }
    }
    private void FixedUpdate()
    {
        perkManager.FixedTick(Time.fixedDeltaTime);
    }

    public void AddPerk(AbilityBase perk)
    {
        perkManager.Apply(perk);
    }
}