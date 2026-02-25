using UnityEngine;

[CreateAssetMenu(menuName = "Characters/Player Character")]
public class PlayerCharacterData : ScriptableObject
{
    [Header("Visual")]
    public GameObject modelPrefab;
    // transform offset from the player's origin
    public Vector3 modelOffset = Vector3.zero;
    public Vector3 modelScale = Vector3.one;
    public Vector3 modelRotation = Vector3.zero;

    [Header("Base Stats")]
    public float maxMoveSpeed = 50f;
    public float acceleration = 25f;
    public float deceleration = 25f;
    public float mass = 1f;

    public float rotationSpeed = 100f;
    public float baseGrip = 10f;
    public float turnGrip = 2.5f;

    public float jumpForce = 125f;

    public float wallRunCooldown = 1f;
    public float wallRunDuration = 2f;
    public float wallRunSpeedMultiplier = 1.1f;

    public float wallJumpUpImpulse = 15f;
    public float wallJumpAwayImpulse = 15f;

    public float dashCooldown = 2f;
    public float dashSpeedBoostMultiplier = 1.15f;
    public float dashBoostDuration = 1.5f;

    [Header("Passive Abilities")]
    public int numOfJumps = 1;
    public float xpMultiplierOnKill = 1f;
}
