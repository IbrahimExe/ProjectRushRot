using UnityEngine;

public class VineTrap : MonoBehaviour
{
    public DebuffType debuffType = DebuffType.Slow;
    public float debuffAmount = 0.4f;
    public float debuffDuration = 3f;

    [Header("Pooling")]
    public string poolName = "ForestVineTrap";
    public bool returnToPoolAfterTrigger = false;

    private void OnTriggerEnter(Collider other)
    {
        PlayerDebuffReceiver receiver = other.GetComponentInParent<PlayerDebuffReceiver>();

        if (receiver == null)
            return;

        receiver.ApplyDebuff(debuffType, debuffAmount, debuffDuration);

        if (returnToPoolAfterTrigger)
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        ObjectPoolManager pool = ServiceLocator.Get<ObjectPoolManager>();

        if (pool != null)
            pool.Return(poolName, gameObject);
        else
            gameObject.SetActive(false);
    }
}