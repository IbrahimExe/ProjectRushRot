using UnityEngine;

public class ZombieDust : MonoBehaviour
{
    private ParticleSystem partSys;
    private bool isPlaying = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        partSys = GetComponent<ParticleSystem>();
        partSys.Stop();
    }

    // Update is called once per frame
    void Update()
    {
        if (!isPlaying && GameState.IsStarted)
        {
            isPlaying = true;
            partSys.Play();
        }
    }
}
