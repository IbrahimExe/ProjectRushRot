using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using TreeEditor;

public class CameraShake : MonoBehaviour {

    [SerializeField] private CinemachineCamera virtualCamera;
    private CinemachineBasicMultiChannelPerlin noise;

    private void Awake()
    {
        if (virtualCamera == null)
        {
            Debug.LogError("CameraShake: virtualCamera reference not assigned in the Inspector.");
            return;
        }

        // GetCinemachineComponent is non-generic; pass the stage and cast the result.
        noise = virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;

        if (noise == null)
        {
            Debug.LogError("CameraShake: virtualCamera has no CinemachineBasicMultiChannelPerlin noise component.");
            return;
        }

        RestartIntensity();
    }


    public void Shake(float intensity, float duration)
    {
        if (noise == null)
        {
            Debug.LogError("CameraShake: Shake() called but noise is null — check earlier Awake error.");
            return;
        }

        noise.AmplitudeGain = intensity;
        StartCoroutine(waitTime(duration));
    }

    IEnumerator waitTime(float ShakeTime)
    {
        yield return new WaitForSeconds(ShakeTime);
        RestartIntensity();
    }

    void RestartIntensity()
    {
        if (noise == null) return;
        noise.AmplitudeGain = 0f;
    }
}
