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
    public FPVector3 position;
    public FPVector3 velocity;
    public FPVector3 depthAxis;
    public FPVector3 currentDirection;
    public FPVector3 lookDirection;
    public bool isGrounded;
    public bool isRootMotionActiveThisFrame;

    public PlayerState_Type cachedCurrentState;
    public int stateFrameCounter;
    public int currentActionID;
    public bool isCommandActionTriggered;

    public HurtInfo currentHurtInfo;
    public WakeUp_Type scheduledWakeUpType;
    public bool isFromRoll;
    
    public FP64 sideStepDirection;
    public int currentStunFrames;
    public bool isGroundBouncing;

    public int currentHealth;
    public int hitstopCounter;

    public InputStateTracker inputTrackerState;
    public ActionControllerSnapshot actionControllerState;
    public InputBufferSnapshot inputBufferState;

    public int controllerFrame;
    public InputFlags previousRawFlags;
    public InputFlags accumulatedHitstopFlags;

    public FP64 lastImpactFallSpeed;
}

public interface ISnapshotSync
{
    void ExportState(ref PlayerSnapshot snapshot);
    void ImportState(PlayerSnapshot snapshot);
}