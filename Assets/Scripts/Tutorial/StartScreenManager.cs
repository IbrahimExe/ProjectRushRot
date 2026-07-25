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

    [Header("Post Intro Activation UI")]
    [SerializeField] private GameObject xpBar;
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private GameObject timerText;
    [SerializeField] private GameObject distanceText;
    [SerializeField] private GameObject speedometer;
    [SerializeField] private GameObject dashUI;

    [SerializeField] private GameObject StartPanel;
    [SerializeField] private float StartPanelTime = 1.0f;

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

        timerPanel.SetActive(true);
        timerPanel.SetActive(false);
        distanceText.SetActive(false);
        distanceText.SetActive(false);
        speedometer.SetActive(false);
        dashUI.SetActive(false);
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
        yield return new WaitForSeconds(blendDuration);

        UnlockPlayer();
    }

    void UnlockPlayer()
    {
        if (!tutorialMode)
        {
            StartCoroutine(ShowStartPanelAndContinue());
        }
        else
        {
            ActivateGameplayUI();
        }
    }
    private IEnumerator ShowStartPanelAndContinue()
    {
        StartPanel.SetActive(true);

        yield return new WaitForSeconds(StartPanelTime);

        StartPanel.SetActive(false);
        ActivateGameplayUI();
    }

    private void ActivateGameplayUI()
    {
        if (!tutorialMode)
        {
            distanceText.SetActive(true);
            timerPanel.SetActive(true);
            timerText.SetActive(true);
        }
        xpBar.SetActive(true);
        speedometer.SetActive(true);
        dashUI.SetActive(true);

        GameState.StartGame();
    }

}