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

    public void DestroyByAbility(string abilityId, GameObject source)
    {
       // Debug.LogError($"DestroyByAbility called with abilityId={abilityId}");

        if (!CanBeDestroyedBy(abilityId))
        {
           // Debug.LogError("CanBeDestroyedBy returned false — exiting early.");
            return;
        }
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
        CameraShake camShake = ServiceLocator.Get<CameraShake>();
        Debug.LogError($"CameraShake service retrieved: {camShake != null}");
        if (camShake != null)
        {
            camShake.Shake(camShakeMagnitude, camShakeDurationSEC);
            Debug.LogError($"Camera shake triggered with magnitude={camShakeMagnitude} and duration={camShakeDurationSEC}");
        }
        //Particles

        //Audio
    }
}