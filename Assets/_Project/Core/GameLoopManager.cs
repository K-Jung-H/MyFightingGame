using UnityEngine;

public class GameLoopManager : MonoBehaviour
{
    [Header("Player 1 Settings")]
    [SerializeField] private CharacterVisual playerOneVisual;
    [SerializeField] private PlayerConfigSO playerOneConfig;
    [SerializeField] private CommandListSO playerOneCommandList;
    [SerializeField] private ComboTreeSO playerOneComboTree;

    [Header("Player 2 Settings")]
    [SerializeField] private CharacterVisual playerTwoVisual;
    [SerializeField] private PlayerConfigSO playerTwoConfig;
    [SerializeField] private CommandListSO playerTwoCommandList;
    [SerializeField] private ComboTreeSO playerTwoComboTree;

    private LocalInputProvider inputProvider;
    private PlayerStateMachine playerOneStateMachine;
    private PlayerStateMachine playerTwoStateMachine;
    
    private int currentTick;
    private bool isSimulationRunning;

    public int GetCurrentTick() => currentTick;
    public PlayerState GetP1State() => playerOneStateMachine != null ? playerOneStateMachine.GetCurrentState() : PlayerState.Idle;
    public Vector3 GetP1Pos() => playerOneStateMachine != null ? playerOneStateMachine.GetPosition() : Vector3.zero;
    public PlayerState GetP2State() => playerTwoStateMachine != null ? playerTwoStateMachine.GetCurrentState() : PlayerState.Idle;
    public Vector3 GetP2Pos() => playerTwoStateMachine != null ? playerTwoStateMachine.GetPosition() : Vector3.zero;

    private void Awake()
    {
        InitializePlayers();
    }

    private void InitializePlayers()
    {
        inputProvider = new LocalInputProvider();

        if (playerOneVisual != null)
        {
            playerOneStateMachine = new PlayerStateMachine();
            playerOneStateMachine.Initialize(
                new Vector3(-2, 0, 0), 
                playerOneConfig, 
                playerOneCommandList, 
                playerOneComboTree
                );
        }

        if (playerTwoVisual != null)
        {
            playerTwoStateMachine = new PlayerStateMachine();
            playerTwoStateMachine.Initialize(
                new Vector3(2, 0, 0), 
                playerTwoConfig, 
                playerTwoCommandList, 
                playerTwoComboTree
                );
        }

        if (playerOneStateMachine != null)
        {
            playerOneStateMachine.SetTarget(playerTwoStateMachine);
        }

        if (playerTwoStateMachine != null)
        {
            playerTwoStateMachine.SetTarget(playerOneStateMachine);
        }

        currentTick = 0;
        isSimulationRunning = true;
    }

    private void FixedUpdate()
    {
        if (isSimulationRunning)
        {
            RunTick();
        }
    }

    private void RunTick()
    {
        if (playerOneStateMachine != null)
        {
            PlayerInput p1Input = inputProvider.GetCurrentInput(currentTick, 0);
            playerOneStateMachine.UpdateTick(p1Input);
        }

        if (playerTwoStateMachine != null)
        {
            PlayerInput p2Input = inputProvider.GetCurrentInput(currentTick, 1);
            playerTwoStateMachine.UpdateTick(p2Input);
        }

        ResolvePlayerCollision();

        SyncVisuals();

        currentTick++;
    }
    
    private float GetPushbackWeight(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Sprinting: return 0.0f;
            case PlayerState.Running: return 0.2f;
            case PlayerState.Walking: return 0.5f;
            case PlayerState.Idle: return 1.0f;
            case PlayerState.Stun: return 1.5f;
            default: return 1.0f;
        }
    }

    private void ResolvePlayerCollision()
    {
        if (playerOneStateMachine == null || playerTwoStateMachine == null) return;

        Vector3 p1Pos = playerOneStateMachine.GetPosition();
        Vector3 p2Pos = playerTwoStateMachine.GetPosition();
        
        Vector3 diff = p1Pos - p2Pos;
        diff.y = 0;
        float distanceSqr = diff.sqrMagnitude;
        float minDistance = 1.0f;

        if (distanceSqr < minDistance * minDistance && distanceSqr > 0.0001f)
        {
            float distance = Mathf.Sqrt(distanceSqr);
            float totalPushDist = minDistance - distance;
            Vector3 pushDir = diff / distance;

            PlayerState p1State = playerOneStateMachine.GetCurrentState();
            PlayerState p2State = playerTwoStateMachine.GetCurrentState();

            float w1 = GetPushbackWeight(p1State);
            float w2 = GetPushbackWeight(p2State);
            float totalWeight = w1 + w2;

            if (totalWeight <= 0.0001f)
            {
                w1 = 0.5f;
                w2 = 0.5f;
                totalWeight = 1.0f;
            }

            float p1Ratio = w1 / totalWeight;
            float p2Ratio = w2 / totalWeight;

            playerOneStateMachine.ApplyPushback(pushDir * (totalPushDist * p1Ratio));
            playerTwoStateMachine.ApplyPushback(-pushDir * (totalPushDist * p2Ratio));
        }
    }

    private void SyncVisuals()
    {
        if (playerOneVisual != null && playerOneStateMachine != null)
        {
            playerOneVisual.SyncWithLogic(
                playerOneStateMachine.GetPosition(), 
                playerOneStateMachine.GetCurrentState(), 
                playerOneStateMachine.GetCurrentSpeed(),
                playerOneStateMachine.GetDirection(),
                playerOneStateMachine.GetLookDirection()
            );
        }

        if (playerTwoVisual != null && playerTwoStateMachine != null)
        {
            playerTwoVisual.SyncWithLogic(
                playerTwoStateMachine.GetPosition(), 
                playerTwoStateMachine.GetCurrentState(), 
                playerTwoStateMachine.GetCurrentSpeed(),
                playerTwoStateMachine.GetDirection(),
                playerTwoStateMachine.GetLookDirection()
            );
        }
    }
}