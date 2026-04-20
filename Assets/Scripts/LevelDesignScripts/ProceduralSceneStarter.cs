using UnityEngine;
using System.Collections;

public class ProceduralSceneStarter : MonoBehaviour
{
    [Header("References - Assign in Inspector")]
    //public RunnerLevelGenerator generator;
    public GameObject player;

    void Awake()
    {
        // Disable the generator temporarily
        //if (generator != null)
        //{
        //    generator.enabled = false;
        //}

        // Disable the player temporarily
        if (player != null)
        {
            player.SetActive(false);
        }

        StartCoroutine(InitializeScene());
    }

    private IEnumerator InitializeScene()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();

        // Re-enable the generator - this triggers its Start()
        //if (generator != null)
        //{
        //    generator.enabled = true;
        //}

        // Wait for generator to finish initial generation
        yield return new WaitForSeconds(1.5f);

        // Re-enable the player
        if (player != null)
        {
            player.SetActive(true);
        }
    }
}