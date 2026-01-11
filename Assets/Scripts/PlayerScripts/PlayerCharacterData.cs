using UnityEngine;

[CreateAssetMenu(menuName = "Characters/Player Character")]
public class PlayerCharacterData : ScriptableObject
{
    [Header("Visual")]
    public GameObject modelPrefab;

    [Header("Base Stats")]
    public float startMoveSpeed = 5f;
    public float maxMoveSpeed = 50f;
    public float acceleration = 25f;
    public float deceleration = 25f;

    public float jumpForce = 125f;

    public float wallJumpUpImpulse = 7.5f;
    public float wallJumpAwayImpulse = 8.5f;

    public float wallRunSpeed = 75f;
    public float wallRunDuration = 4f;

    //[Header("Size and Hitbox")] for bypassing the error for testing

}
