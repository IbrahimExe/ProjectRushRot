using UnityEngine;
using Unity.Cinemachine;

public class StartScreenManager : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera startCamera;
    [SerializeField] private CinemachineCamera gameplayCamera;

    [Header("UI")]
    [SerializeField] private GameObject startScreenUI;

    [Header("Transition")]
    [SerializeField] private float blendDuration = 3.0f;

    private bool hasStarted = false;

    void Start()
    {
        startCamera.Priority = 20;
        gameplayCamera.Priority = 10;
        startScreenUI.SetActive(true);
    }

    void Update()
    {
        if (!hasStarted && Input.anyKeyDown)
        {
            hasStarted = true;
            startCamera.Priority = 5;
            gameplayCamera.Priority = 20;
            startScreenUI.SetActive(false);

            // Give the camera blend time to finish, then unlock the player
            Invoke(nameof(UnlockPlayer), blendDuration);
        }
    }

    void UnlockPlayer()
    {
        GameState.StartGame();
    }
}