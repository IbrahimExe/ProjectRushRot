using UnityEngine;

public class PlayerProtector : MonoBehaviour
{
    [SerializeField] private LayerMask[] protectedLayers;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnCollisionEnter(Collision collision)
    {
        // deactivate everything that is not in the protected layers
        foreach (var layer in protectedLayers)
        {
            if (collision.gameObject.layer != layer)
            {
                collision.gameObject.SetActive(false);
            }
        }
    }
}
