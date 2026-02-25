using UnityEngine;

public class CharacterSelector : MonoBehaviour
{
    [Header("Type of character")]
    [SerializeField] private PlayerCharacterData characterData;

    [Header("Model Root")]
    [SerializeField] private Transform modelRoot;

    [Header("Idle Animation")]
    [SerializeField] private float rotationSpeed = 40f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;
    private float baseRotationSpeed;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private GameObject currentModel;
    private Vector3 baseLocalPos;
    private float bobTimer;

    private PlayerControllerBase playerInside;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        baseRotationSpeed = rotationSpeed;
        SpawnModel();
    }

    // Update is called once per frame
    void Update()
    {
        ModelAnimation();

        if (playerInside != null && Input.GetKeyDown(interactKey))
        {
            playerInside.ChangeCharacter(characterData);
        }
    }
    private void SpawnModel()
    {
        if (characterData == null || characterData.modelPrefab == null)
        {
            return;
        }

        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        currentModel = Instantiate(characterData.modelPrefab, modelRoot);

        Transform t = currentModel.transform;
        t.localPosition = characterData.modelOffset;
        t.localRotation = Quaternion.Euler(characterData.modelRotation);
        t.localScale = characterData.modelScale;

        baseLocalPos = t.localPosition;
    }

    private void ModelAnimation()
    {
        if (currentModel == null)
        {
            return;
        }

        Transform t = currentModel.transform;

        // Rotate slowly
        t.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

        // Bob up & down
        bobTimer += Time.deltaTime * bobSpeed;
        float bobOffset = Mathf.Sin(bobTimer) * bobHeight;

        t.localPosition = baseLocalPos + Vector3.up * bobOffset;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        { return; }

        if (playerInside != null)
        { return; }

        playerInside = other.GetComponent<PlayerControllerBase>();
        if (playerInside == null)
        { return; }

        if (playerInside.characterData == characterData)
        { return; }

        baseRotationSpeed = rotationSpeed;
        rotationSpeed *= 3f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        { return; }

        if (playerInside != null && other.gameObject == playerInside.gameObject)
        {
            rotationSpeed = baseRotationSpeed;
            playerInside = null;
            Debug.Log("Player left character selector");
        }
    }
}
