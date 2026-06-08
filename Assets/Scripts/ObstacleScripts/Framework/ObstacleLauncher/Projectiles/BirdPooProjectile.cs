using UnityEngine;

public class BirdPooProjectile : ProjectileBehavior
{
    [Header("Bird poo settings")]
    [SerializeField] private GameObject poo;
    [SerializeField] private GameObject pooPuddle;
    [SerializeField] private Rigidbody rb;

    private bool hasHit = false;

    private void OnEnable()
    {
        ResetPoo();
    }

    public override bool OnHit(Collision collision)
    {
        Collision();

        return false; // don't return on collision it will have lifetime
    }

    public void Collision()
    {
        poo.SetActive(false);
        pooPuddle.SetActive(true);
        rb.isKinematic = true;
    }

    public void ResetPoo()
    {
        poo.SetActive(true);
        pooPuddle.SetActive(false);
        rb.isKinematic = false;
    }
}