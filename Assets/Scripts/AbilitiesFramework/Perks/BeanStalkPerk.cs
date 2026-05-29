using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Perks/Beanstalk")]
public class BeanStalkPerk : AbilityBase
{
    [Header("Curve Pieces")]
    public int basePieces = 32;
    public int piecesPerLevel = 4;

    public float pieceWidth = 6f;
    public float pieceThickness = 0.75f;
    public float pieceLength = 2.5f;

    public float totalRise = 7f;
    public float curveHeightBoost = 2f;

    public float segmentLifetime = 5f;

    [Header("Start Placement")]
    public float startBehindPlayer = 1.5f;
    public float startBelowPlayer = 0.75f;

    [Header("Flat Side Curve")]
    public float totalForwardDistance = 36f;
    public float curveSideOffset = 5f;

    [Header("Cooldown")]
    public float baseCooldown = 8f;
    public float cooldownReductionPerLevel = 0.5f;
    public float minimumCooldown = 3f;

    private float cooldownTimer;
    private readonly List<GameObject> activePieces = new();

    public override void Tick(PlayerAbilityContext ctx, int level, float deltaTime)
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= deltaTime;
    }

    public override bool TryUse(PlayerAbilityContext ctx, int level)
    {
        if (cooldownTimer > 0f)
            return false;

        ObjectPoolManager poolManager = ServiceLocator.Get<ObjectPoolManager>();

        //if (poolManager == null)
        //{
        //    Debug.LogError("ObjectPoolManager service not found.");
        //    return false;
        //}

        ClearOldPieces();

        int count = Mathf.Max(4, basePieces + piecesPerLevel * (level - 1));

        Vector3 forward = ctx.playerTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = ctx.playerTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 start =
            ctx.playerTransform.position
            - forward * startBehindPlayer
            - Vector3.up * startBelowPlayer;

        Vector3 p0 = start;

        Vector3 p1 =
            start
            + forward * (totalForwardDistance * 0.25f)
            + Vector3.up * curveHeightBoost
            + right * curveSideOffset;

        Vector3 p2 =
            start
            + forward * (totalForwardDistance * 0.70f)
            + Vector3.up * totalRise
            - right * curveSideOffset;

        Vector3 p3 =
            start
            + forward * totalForwardDistance
            + Vector3.up * totalRise;

        for (int i = 0; i < count; i++)
        {
            float t1 = i / (float)count;
            float t2 = (i + 1) / (float)count;

            Vector3 a = GetBezierPoint(p0, p1, p2, p3, t1);
            Vector3 b = GetBezierPoint(p0, p1, p2, p3, t2);

            Vector3 center = (a + b) * 0.5f;

            Vector3 direction = b - a;

            if (direction.magnitude <= 0.01f)
                continue;

            direction.Normalize();

            // Keeps the top side upright instead of rolling/twisting
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            GameObject piece = poolManager.Get("Beanstalk", center, rotation);

            if (piece == null)
                continue;

            piece.transform.localScale = new Vector3(
                pieceWidth,
                pieceThickness,
                pieceLength
            );

            if (piece.GetComponent<BeanstalkSurface>() == null)
                piece.AddComponent<BeanstalkSurface>();

            activePieces.Add(piece);
            ctx.player.StartCoroutine(ReturnAfterDelay(piece, segmentLifetime));
        }

        cooldownTimer = GetCooldown(level);
        return true;
    }

    private Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;

        return
            u * u * u * p0 +
            3f * u * u * t * p1 +
            3f * u * t * t * p2 +
            t * t * t * p3;
    }

    private float GetCooldown(int level)
    {
        return Mathf.Max(minimumCooldown, baseCooldown - cooldownReductionPerLevel * (level - 1));
    }

    private void ClearOldPieces()
    {
        for (int i = activePieces.Count - 1; i >= 0; i--)
        {
            if (activePieces[i] == null)
                continue;

            PooledObject pooled = activePieces[i].GetComponent<PooledObject>();

            if (pooled != null)
                pooled.ReturnToPool();
            else
                activePieces[i].SetActive(false);
        }

        activePieces.Clear();
    }

    private System.Collections.IEnumerator ReturnAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj == null)
            yield break;

        ObjectPoolManager poolManager = ServiceLocator.Get<ObjectPoolManager>();

        if (poolManager != null)
            poolManager.Return("Beanstalk", obj);
        else
            obj.SetActive(false);

        activePieces.Remove(obj);
    }
}