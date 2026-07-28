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
    [SerializeField] private CameraShake camShake;
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

    public void DestroyByAbility(string abilityId, GameObject source)
    {
        if (!CanBeDestroyedBy(abilityId))
            return;

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