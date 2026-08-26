using UnityEngine;

public class DeactivationEffect : MonoBehaviour
{
    [SerializeField] private string effectPoolName;

    private bool _suppressNextDisable = false;
    private static bool _isQuitting = false;

    private void OnEnable()
    {
        Application.quitting -= HandleQuitting; // avoid double subscription
        Application.quitting += HandleQuitting;
    }

    private void OnDestroy()
    {
        Application.quitting -= HandleQuitting;
    }

    private static void HandleQuitting()
    {
        _isQuitting = true;
    }

    public void SuppressNextDisable()
    {
        _suppressNextDisable = true;
    }

    private void OnDisable()
    {
        if (_isQuitting) return;

        if (_suppressNextDisable)
        {
            _suppressNextDisable = false;
            return;
        }

        ObjectPoolManager poolManager = ServiceLocator.Get<ObjectPoolManager>();
        if (poolManager == null) return;

        GameObject effect = poolManager.Get(effectPoolName, transform.position, Quaternion.identity);
        if (effect == null) return;

        ParticleSystem ps = effect.GetComponentInChildren<ParticleSystem>();
        ps.Play(true);

        poolManager.StartCoroutine(ReturnWhenFinished(poolManager, ps, effect));
    }

    private System.Collections.IEnumerator ReturnWhenFinished(ObjectPoolManager poolManager, ParticleSystem ps, GameObject effect)
    {
        while (ps.IsAlive(true))
        {
            yield return null;
        }

        poolManager.Return(effectPoolName, effect);
    }
}