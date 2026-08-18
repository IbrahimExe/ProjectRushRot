using UnityEngine;

public class DirectionalArrow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer arrowRenderer;

    [Header("Materials")]
    [SerializeField] private Material greenMaterial;
    [SerializeField] private Material yellowMaterial;
    [SerializeField] private Material redMaterial;

    [Header("Settings")]
    [SerializeField] private float greenAngle = 30f;

    private void Update()
    {
        UpdateArrowDirection();
        UpdateArrowColor();
    }

    private void UpdateArrowDirection()
    {
        transform.rotation = Quaternion.identity;
    }

    private void UpdateArrowColor()
    {
        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        // World +Z is the desired forward direction.
        Vector3 desiredForward = Vector3.forward;

        float dot = Vector3.Dot(playerForward, desiredForward);

        float greenThreshold = Mathf.Cos(greenAngle * Mathf.Deg2Rad);

        if (dot >= greenThreshold)
        {
            arrowRenderer.material = greenMaterial;
        }
        else if (dot >= 0f)
        {
            arrowRenderer.material = yellowMaterial;
        }
        else
        {
            arrowRenderer.material = redMaterial;
        }
    }
}