using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class StartScreenManager : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera startCamera;
    [SerializeField] private CinemachineCamera gameplayCamera;

    [Header("UI")]
    [SerializeField] private GameObject startScreenUI;

    [Header("Transition")]
    [SerializeField] private float timeForTransitionToStart = 2.0f;
    [SerializeField] private float blendDuration = 3.0f;

    [SerializeField] private bool tutorialMode = true;
    private CinemachineBrain cinemachineBrain;

    private bool hasStarted = false;

    void Start()
    {
        startCamera.Priority = 20;
        gameplayCamera.Priority = 10;
        cinemachineBrain = FindFirstObjectByType<CinemachineBrain>();
        if (tutorialMode)
        {
            startScreenUI.SetActive(true);
        }
    }

    void Update()
    {
        if (tutorialMode)
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
        else
        {
            if (!hasStarted)
            {
                if (timeForTransitionToStart <= 0)
                {
                    hasStarted = true;
                    //startCamera.Priority = 5;
                    //gameplayCamera.Priority = 20;

                    ////Invoke(nameof(UnlockPlayer), blendDuration);
                    //StartCoroutine(WaitForCameraBlendCompletion());
                    TriggerCameraTransition();
                }
                else
                {
                   
                    timeForTransitionToStart -= Time.deltaTime;
                    
                }
            }
        
        }
    }

    private void TriggerCameraTransition()
    {
        // Set the blend duration on the brain BEFORE changing priorities
        if (cinemachineBrain != null)
        {
            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                blendDuration
            );
        }

        startCamera.Priority = 5;
        gameplayCamera.Priority = 20;
        startScreenUI.SetActive(false);

        // Wait for blend to complete, then unlock player
        StartCoroutine(WaitForCameraBlendCompletion());
    }

    private IEnumerator WaitForCameraBlendCompletion()
    {
        // Wait for the exact blend duration. 
        // Relying on cinemachineBrain.IsBlending can be unreliable on scene restarts 
        // because the Brain might not have processed the camera change yet in its update cycle.
        yield return new WaitForSeconds(blendDuration);

        UnlockPlayer();
    }

    void UnlockPlayer()
    {
        GameState.StartGame();
    }
}