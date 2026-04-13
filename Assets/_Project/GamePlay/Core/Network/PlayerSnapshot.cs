public unsafe struct ActionControllerSnapshot
{
    public fixed int comboSequence[10];
    public int comboCount;
    public BufferedAction? pendingAction;
    public DeterministicInputBuffer deterministicInputBuffer;
}

public unsafe struct CombatStateSnapshot
{
    public fixed int registeredHitGroups[10];
    public int hitGroupCount;
}

public unsafe struct InputBufferSnapshot
{
    public int head;
    public int count;
    public fixed int frames[60];
    public fixed int rawFlags[60];
}

public unsafe struct PlayerSnapshot
{
    public FPVector3 position;
    public FPVector3 velocity;
    public FPVector3 depthAxis;
    public FPVector3 currentDirection;
    public FPVector3 lookDirection;
    public bool isGrounded;
    public bool isRootMotionActiveThisFrame;

    public PlayerState_Type cachedCurrentState;
    public PlayerState_Type previousStateType;
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

    public InputBufferSnapshot inputBufferState;
    public InputStateTracker inputTrackerState;
    public ActionControllerSnapshot actionControllerState;
    public CombatStateSnapshot combatState;

    public int controllerFrame;
    public InputFlags previousRawFlags;
    public InputFlags accumulatedHitstopFlags;
    public InputFlags accumulatedLogicFlags;
    public FP64 lastImpactFallSpeed;
}

public interface ISnapshotSync
{
    void ExportState(ref PlayerSnapshot snapshot);
    void ImportState(PlayerSnapshot snapshot);
}