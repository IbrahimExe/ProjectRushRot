using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ProceduralLoader : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(LoadProcedural());
    }

    private IEnumerator LoadProcedural()
    {
        // Force complete memory cleanup
        Resources.UnloadUnusedAssets();
        System.GC.Collect();

        yield return new WaitForSeconds(0.3f);

        // Load the procedural scene fresh
        SceneManager.LoadScene("ProceduralLevel");
    }
}