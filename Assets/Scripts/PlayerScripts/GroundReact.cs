using UnityEngine;

public class GroundReact : MonoBehaviour
{
    private PlayerControllerBase controller;
    private ParticleSystem[] activeParticle;
    private string currentRegion;

    [Header("Ground Particles")]
    public ParticleSystem[] waterSplash;
    public ParticleSystem[] grassTrail;
    public ParticleSystem[] sandTrail;
    public ParticleSystem[] StoneTrail;
    public ParticleSystem[] ForestTrail;

    void Start() {
    
    }

    void Update()
    {
        string newRegion = GetRegionFromPlayer();
        if (newRegion != currentRegion)
        {
            currentRegion = newRegion;
            HandleRegionChange(currentRegion);
        }
    }

    private void HandleRegionChange(string region)
    {
         
        ParticleSystem[] newParticle = null;

        switch (region)
        {
                case "GRASS":
                Debug.Log("Player is now on Grass. Playing grass particle.");
                 newParticle = grassTrail;
                break;

                case "SAND":
                Debug.Log("Player is now on Sand. Playing sand particle.");
                newParticle = sandTrail;
                break;

                case "WATER":
                Debug.Log("Player is now on Water. Playing water particle.");
                newParticle = waterSplash;
                OutOfBoundsRespawn respawn = GetComponentInParent<OutOfBoundsRespawn>();
                break;

                case "STONE":
                Debug.Log("Player is now on Stone. Playing stone particle.");
                newParticle = StoneTrail;
                break;

                case "FOREST":
                Debug.Log("Player is now in a Forest. Playing forest particle.");
                newParticle = ForestTrail;
                break;

            default:
                Debug.Log("Player is now on an unknown surface. Remember to tag your regions");
                break;
        }

        ChangeParles(activeParticle, newParticle);
        activeParticle = newParticle;
    }

    private string GetRegionFromPlayer()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<PlayerControllerBase>();
        }
        if (controller != null)
        {
            currentRegion = controller.lastGroundRegion;
        }

        return currentRegion;
    }

    private void ChangeParles(ParticleSystem[] currentParticle, ParticleSystem[] newParticle)
    {
        if (currentParticle != null)
            foreach (ParticleSystem ps in currentParticle)
                if (ps != null && ps.isPlaying) ps.Stop();

        if (newParticle != null)
            foreach (ParticleSystem ps in newParticle)
                if (ps != null) ps.Play();
    }
}
