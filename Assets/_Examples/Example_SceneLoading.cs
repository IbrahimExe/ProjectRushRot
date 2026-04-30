/*
 * Scene loading should take place after the AppLoader has completed loading systems in Global scope
 * The Scene specific loader can maintain references to all GameObjects and Monobehaviours in the scene, and control the order in which
 * these systems are initialized.
 * Monobehaviours in the scene should avoid using Awake/Start to initialize themselves and instead provide a public "Initialize" method
 * that can be called by the Scene loader. This prevents processes from starting before all dependencies are initialized
 */

using System;
using UnityEngine;

public class Example_SceneLoading : MonoBehaviour
{
    // References to any GameObject or Monobehaviour in the scene should be placed here through SerializedField private variables
    // DO NOT make these variables public by default. Maintain SOLID programming principles.
    [SerializeField] private GameObject _playerGO;


    // If you need to get a reference to any of the private variables serialized above you can provide a public property here
    public GameObject Player => _playerGO;

    // Member variables of the class should go here, including any refernces to global systems/managers that you need.


    // Use 'Start' here to register a callback with the AppLoader to be called when the process of loading Global systems is complete.
    private void Start()
    {
        SystemLoader.CallOnComplete(Initialize);
    }

    // Initialize will be called when the AppLoader is finished loading global systems and registering them with the ServiceLocator
    // This allows access to any of the global systems through the ServiceLocator.Get<> method.
    private void Initialize()
    {
        // Fetch references to any globals you need

        // Initialize scene elements

        // Start any processes that update every frame such as gameplay.

    }
}
