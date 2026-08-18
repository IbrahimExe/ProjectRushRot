using UnityEngine;

public interface IAbilityDestructible
{
    bool CanBeDestroyedBy(string abilityId);
    void DestroyByAbility(string abilityId, GameObject source);
}

public class AbilityDestructible : MonoBehaviour, IAbilityDestructible
{
    [SerializeField] private string[] destroyableByAbilities;

    [Header("Pooling")]
    [SerializeField] private bool returnToPoolInsteadOfDestroy = false;

    private PooledObject pooledObject;

    [Header("CamShake Param")]
    [SerializeField] private float camShakeDurationSEC = 0.5f;
    [SerializeField] private float camShakeMagnitude = 1f;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    public bool CanBeDestroyedBy(string abilityId)
    {
        foreach (string id in destroyableByAbilities)
        {
            if (id == abilityId)
                return true;
        }

        return false;
    }

    private bool destroyedThisSpawn;

    private void OnEnable()
    {
        destroyedThisSpawn = false;
    }
    public void DestroyByAbility(string abilityId, GameObject source)
    {
        if (!CanBeDestroyedBy(abilityId))
            return;

        if (destroyedThisSpawn)
            return;

        destroyedThisSpawn = true;

        // Count this enemy/obstacle toward the score
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterDestroyedTarget();
        }
        else
        {
            Debug.LogWarning("AbilityDestructible: ScoreManager.Instance was not found.");
        }

        if (returnToPoolInsteadOfDestroy && pooledObject != null)
        {
            pooledObject.ReturnToPool();
        }
        else
        {
            gameObject.SetActive(false);
        }

        // CamShake
        CameraShake camShake = ServiceLocator.Get<CameraShake>();

        if (camShake != null)
        {
            camShake.Shake(camShakeMagnitude, camShakeDurationSEC);
        }

        // Particles

        // Audio
    }
}