using UnityEngine;

public interface IAbilityDestructible
{
    bool CanBeDestroyedBy(string abilityId);
    void DestroyByAbility(string abilityId, GameObject source);
}

public class AbilityDestructible : MonoBehaviour, IAbilityDestructible
{
    [SerializeField] private string[] destroyableByAbilities;
    [SerializeField] private GameObject destroyVFX;

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

        if (destroyVFX != null)
            Instantiate(destroyVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}