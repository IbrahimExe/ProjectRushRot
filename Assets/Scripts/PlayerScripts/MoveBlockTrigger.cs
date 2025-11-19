using UnityEngine;

public class MoveBlockTrigger : MonoBehaviour
{
    public enum BlockType { Forward, Backward }
    public BlockType blockType;

    private GPlayerController movement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        movement = GetComponentInParent<GPlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Wall"))
        {
            if (blockType == BlockType.Forward)
            {
                movement.BlockForwardMovement();
            }
            else if (blockType == BlockType.Backward)
            {
                movement.BlockBackwardMovement();
            }
        }
    }
}
