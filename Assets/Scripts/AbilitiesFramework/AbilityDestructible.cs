using UnityEngine;

public interface IAbilityDestructible
{
    bool CanBeDestroyedBy(string abilityId);
    void DestroyByAbility(string abilityId, GameObject source);
}

[DisallowMultipleComponent]
public class AbilityDestructible : MonoBehaviour, IAbilityDestructible
{
    [SerializeField] private string[] destroyableByAbilities;

    [Header("Pooling")]
    [SerializeField] private bool returnToPoolInsteadOfDestroy = false;

    private PooledObject pooledObject;

    [Header("CamShake Param")]
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float camShakeDurationSEC = 0.5f;
    [SerializeField] private float camShakeMagnitude = 1f;

    private bool hasBeenDestroyed;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    private void OnEnable()
    {
        
        hasBeenDestroyed = false;
    }

    public bool CanBeDestroyedBy(string abilityId)
    {
        if (hasBeenDestroyed)
            return false;

        if (string.IsNullOrEmpty(abilityId))
            return false;

        if (destroyableByAbilities == null)
            return false;

        foreach (string id in destroyableByAbilities)
        {
            if (id == abilityId)
                return true;
        }

        return false;
    }

    public void DestroyByAbility(string abilityId, GameObject source)
    {
        if (!CanBeDestroyedBy(abilityId))
            return;

       
        hasBeenDestroyed = true;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterDestroyedTarget();
        }
        else
        {
            Debug.LogWarning(
                $"AbilityDestructible on {name} could not find ScoreManager."
            );
        }

        if (returnToPoolInsteadOfDestroy && pooledObject != null)
        {
            pooledObject.ReturnToPool();
        }
        else
        {
            //Destroy(gameObject);

            gameObject.SetActive(false);
        }


        //CamShake
        camShake.Shake(camShakeMagnitude, camShakeDurationSEC);
        //Particles

        //Audio
    }
}