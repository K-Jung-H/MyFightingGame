using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "ScriptableObjects/PlayerConfig")]
public class PlayerConfigSO : ScriptableObject
{
    [Header("Movement Speeds")]
    public float walkSpeed = 0.1f;
    public float runSpeed = 0.18f;
    public float sprintSpeed = 0.25f;
    public float turnLerpSpeed = 0.2f;

    [Header("Input Settings")]
    public int tapWindowFrames = 15;
    public int autoSprintFrames = 60;
    
    [Header("State Settings")]
    public int attackFrameLimit = 30;
}