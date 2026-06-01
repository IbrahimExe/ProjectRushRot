using UnityEngine;

public class SlowingObstacle : MonoBehaviour
{
    [SerializeField] private DebuffType debuffType = DebuffType.Slow;
    [SerializeField] private float debuffAmount = 0.3f;
    [SerializeField] private float debuffDuration = 3f;

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other.gameObject);
    }
    private void TryHit(GameObject hitObject)
    {
        PlayerDebuffReceiver receiver = hitObject.GetComponentInParent<PlayerDebuffReceiver>();

        if (receiver == null)
            return;

        receiver.ApplyDebuff(debuffType, debuffAmount, debuffDuration);

    }
}
