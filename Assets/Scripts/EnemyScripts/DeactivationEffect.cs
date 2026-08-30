using UnityEngine;

public class DeactivationEffect : MonoBehaviour
{
    [SerializeField] private string effectPoolName;

    private bool _suppressNextDisable = false;
    private static bool _isQuitting = false;

    private void OnEnable()
    {
        _suppressNextDisable = false;
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
        if (_isQuitting) return; // Do not spawn effects when the application is quitting

        if (_suppressNextDisable) // Do not spawn the effect if SuppressNextDisable was called
        {
            _suppressNextDisable = false;
            return;
        }

        // check if the ObjectPoolManager is available
        if (ServiceLocator.Get<ObjectPoolManager>() == null)
        {
            return;
        }
        ObjectPoolManager poolManager = ServiceLocator.Get<ObjectPoolManager>();
        if (poolManager == null) return;

        GameObject effect = poolManager.Get(effectPoolName, transform.position, Quaternion.identity);
        if (effect == null) return;

        ParticleSystem ps = effect.GetComponentInChildren<ParticleSystem>();
        ps.Play(true);
        // debug what object is spawning the effect
        //Debug.Log($"DeactivationEffect: Spawning effect '{effectPoolName}' at position {transform.position} for object '{gameObject.name}'");

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

    public void SuppressAndDeactivate()
    {
        _suppressNextDisable = true;
        gameObject.SetActive(false);
    }
}