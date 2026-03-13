using UnityEngine;

public struct ActionControllerSnapshot
{
    public InputFlags[] comboSequence;
    public int comboCount;
    public BufferedAction? pendingAction;
}

public struct InputBufferSnapshot
{
    public PlayerInput[] inputs;
    public int head;
    public int count;
}

public struct PlayerSnapshot
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 depthAxis;
    public Vector3 currentDirection;
    public Vector3 lookDirection;
    public bool isGrounded;
    public bool isRootMotionActiveThisFrame;

    public PlayerState_Type cachedCurrentState;
    public int stateFrameCounter;
    public int currentActionID;
    public bool isCommandActionTriggered;

    public HurtInfo currentHurtInfo;
    public WakeUp_Type scheduledWakeUpType;
    public bool isFromRoll;

    public int currentHealth;
    public int hitstopCounter;

    public InputStateTracker inputTrackerState;
    public ActionControllerSnapshot actionControllerState;
    public InputBufferSnapshot inputBufferState;

    public int controllerFrame;
    public InputFlags previousRawFlags;
    public InputFlags accumulatedHitstopFlags;

    public float lastImpactFallSpeed;
}

public interface ISnapshotSync
{
    void ExportState(ref PlayerSnapshot snapshot);
    void ImportState(PlayerSnapshot snapshot);
}