using UnityEngine;
using System.Collections;

public class EnemyDistanceCulling : MonoBehaviour
{
    [SerializeField] public Transform player;
    [SerializeField] public float disableDistance = 150f;
    [SerializeField] public float checkRate = 2f;

    void Start()
    {
        StartCoroutine(CheckDistance());
    }

    IEnumerator CheckDistance()
    {
        while (true)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            gameObject.SetActive(dist <= disableDistance);
            yield return new WaitForSeconds(checkRate);
        }
    }
}
