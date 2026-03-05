using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    
    [Header("Animation Blend Settings")]
    [SerializeField] private float attackBlendTime = 0f;
    [SerializeField] private float hitBlendTime = 0f;
    [SerializeField] private float commandBlendTime = 0.1f;
    [SerializeField] private float locomotionBlendTime = 0.1f;
    
    private EffectTableSO effectTable;
    private PlayerStateMachine logicMachine;
    private StateAnimationMapSO stateAnimMap;
    private Vector3 targetPosition;
    private float targetSpeed;
    private Vector3 targetDirection;
    private Vector3 targetLookDirection;
    private float currentSpeed;
    private Vector3 currentDirection;
    private PlayerState_Type previousState;
    
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int LocomotionHash = Animator.StringToHash("Move Blend Tree");

    public void InitializeVisual(PlayerStateMachine stateMachine, StateAnimationMapSO stateMap, EffectTableSO fxTable)
    {
        logicMachine = stateMachine;
        stateAnimMap = stateMap;
        effectTable = fxTable;
        previousState = (PlayerState_Type)(-1);
    }

    public void PlayHitSpark(Vector3 hitPosition, EffectType effectType)
    {
        VfxClipSO resolvedClip = effectTable != null ? effectTable.GetClip(effectType) : null;
        
        bool isClipValid = resolvedClip != null;
        if (isClipValid)
        {
            VfxManager.Instance.SpawnVfxAtPosition(resolvedClip, hitPosition, Quaternion.identity);
        }
    }

    public void UpdateVisual()
    {
        if (logicMachine == null) return;

        bool isHitstopActive = logicMachine.GetHitstopCounter() > 0;

        if (characterAnimator != null)
        {
            characterAnimator.speed = isHitstopActive ? 0f : 1f;
        }

        if (!isHitstopActive)
        {
            SyncTransformWithLogic();
            EvaluateAndPlayAnimation();
        }
    }

    private void EvaluateVfxEvents()
    {
        ActionDataSO currentAction = logicMachine.GetCurrentActionData();
        bool isActionInvalid = currentAction == null || currentAction.frameData.vfxEvents == null;
        if (isActionInvalid) return;

        int currentFrame = logicMachine.GetStateFrameCounter();

        foreach (var vfxEvent in currentAction.frameData.vfxEvents)
        {
            bool isWithinRange = currentFrame >= vfxEvent.startFrame && currentFrame <= vfxEvent.endFrame;
            if (!isWithinRange) continue;

            bool isSpawnFrame = false;
            bool isSingleSpawn = vfxEvent.intervalFrames <= 0;
            
            if (isSingleSpawn)
            {
                isSpawnFrame = currentFrame == vfxEvent.startFrame;
            }
            else
            {
                isSpawnFrame = (currentFrame - vfxEvent.startFrame) % vfxEvent.intervalFrames == 0;
            }

            if (isSpawnFrame)
            {
                Transform targetBoneTransform = characterAnimator.GetBoneTransform(vfxEvent.targetBone);
                VfxClipSO resolvedClip = effectTable != null ? effectTable.GetClip(vfxEvent.effectType) : null;
                
                bool isSpawnValid = targetBoneTransform != null && resolvedClip != null;
                if (isSpawnValid)
                {
                    VfxManager.Instance.SpawnVfx(
                        resolvedClip, 
                        targetBoneTransform, 
                        vfxEvent.localPositionOffset, 
                        vfxEvent.localRotationOffset, 
                        vfxEvent.isAttached
                    );
                }
            }
        }
    }

    private void SyncTransformWithLogic()
    {
        targetPosition = logicMachine.GetPosition();
        targetSpeed = logicMachine.GetCurrentSpeed();
        targetDirection = logicMachine.GetDirection();
        targetLookDirection = logicMachine.GetLookDirection();
    }

    private void EvaluateAndPlayAnimation()
    {
        PlayerState_Type currentState = logicMachine.GetCurrentState();
        int stateFrame = logicMachine.GetStateFrameCounter();
        
        bool isStateChanged = previousState != currentState;

        bool isHitState = currentState == PlayerState_Type.StandHit || 
                          currentState == PlayerState_Type.AirHit || 
                          currentState == PlayerState_Type.Stunning || 
                          currentState == PlayerState_Type.GroundSmash ||
                          currentState == PlayerState_Type.LayingDown ||
                          currentState == PlayerState_Type.WakeUp;

        if (isHitState && stateFrame == 1)
        {
            PlayHitAnimation(currentState);
        }
        else if (logicMachine.CheckAndConsumeCommandAction(out int commandHash))
        {
            characterAnimator.CrossFadeInFixedTime(commandHash, commandBlendTime, 0);
        }
        else if (currentState == PlayerState_Type.Attacking && stateFrame == 1)
        {
            PlayAttackAnimation();
        }
        else if (isStateChanged)
        {
            EvaluateLocomotionTransition(currentState);
        }

        EvaluateVfxEvents();
        previousState = currentState;
    }

    private void PlayHitAnimation(PlayerState_Type currentState)
    {
        string mappedAnimName = currentState.ToString();
        bool isStateMapValid = stateAnimMap != null;
        
        bool isWakeUpState = currentState == PlayerState_Type.WakeUp;
        bool isLayingDownState = currentState == PlayerState_Type.LayingDown;

        if (isWakeUpState)
        {
            WakeUpState wakeUpState = logicMachine.GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;
            bool isWakeUpStateValid = wakeUpState != null;

            WakeUp_Type currentWakeUpType = isWakeUpStateValid ? wakeUpState.GetScheduledWakeUpType() : WakeUp_Type.InPlace;
            
            if (isStateMapValid)
            {
                mappedAnimName = stateAnimMap.GetWakeUpAnimationName(currentWakeUpType);
            }
            else
            {
                mappedAnimName = $"WakeUp_{currentWakeUpType}";
            }
        }
        else if (isLayingDownState)
        {
            LayingDownState layState = logicMachine.GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
            bool isLayStateValid = layState != null;
            bool isFromRoll = isLayStateValid && layState.IsFromRoll();

            if (isStateMapValid)
            {
                mappedAnimName = stateAnimMap.GetLayingDownAnimationName(isFromRoll);
            }
            else
            {
                mappedAnimName = isFromRoll ? "LayingDown_Idle" : "LayingDown_Initial";
            }
        }
        else if (isStateMapValid)
        {
            mappedAnimName = stateAnimMap.GetStateAnimationName(currentState);
        }
        
        int finalHash = logicMachine.GetAnimationHash(mappedAnimName);
        SafeCrossFade(finalHash, mappedAnimName, hitBlendTime, "Hit/State");
    }

    private void PlayAttackAnimation()
    {
        int finalHash = logicMachine.GetCurrentAttackTriggerHash();
        bool isInvalidHash = finalHash == 0;

        if (isInvalidHash)
        {
            Debug.LogWarning("[Animation Warning] 실행 시도한 공격의 애니메이션 해시가 0입니다.");
            return;
        }

        var actionData = logicMachine.GetCurrentActionData();
        string actionInfo = actionData != null ? $"{actionData.name} (State: {actionData.animationStateName})" : "Unknown Action";
        
        SafeCrossFade(finalHash, actionInfo, attackBlendTime, "Attack");
    }

    private void SafeCrossFade(int hash, string debugName, float blendTime, string category)
    {
        bool hasState = characterAnimator.HasState(0, hash);

        if (hasState)
        {
            Debug.Log($"[Animation Play] {category}: {debugName}");
            characterAnimator.CrossFadeInFixedTime(hash, blendTime, 0);
        }
        else
        {
            Debug.LogError($"[Animation Missing] {category} 애니메이션 '{debugName}'이 컨트롤러에 없습니다. (Hash: {hash})");
        }
    }

    private void EvaluateLocomotionTransition(PlayerState_Type currentState)
    {
        bool isCurrentLocomotion = currentState == PlayerState_Type.Idle || 
                                   currentState == PlayerState_Type.Walking || 
                                   currentState == PlayerState_Type.Running || 
                                   currentState == PlayerState_Type.Sprinting;
            
        bool isPreviousAction = previousState == PlayerState_Type.Attacking || 
                                previousState == PlayerState_Type.StandHit || 
                                previousState == PlayerState_Type.AirHit || 
                                previousState == PlayerState_Type.Stunning || 
                                previousState == PlayerState_Type.GroundSmash ||
                                previousState == PlayerState_Type.LayingDown ||
                                previousState == PlayerState_Type.WakeUp;

        bool shouldTransitionToLocomotion = isCurrentLocomotion && isPreviousAction;
        if (shouldTransitionToLocomotion)
        {
            characterAnimator.CrossFadeInFixedTime(LocomotionHash, locomotionBlendTime, 0);
        }
    }

    private void Update()
    {
        if (logicMachine != null && logicMachine.GetHitstopCounter() > 0) return;

        UpdateTransformInterpolation();
        UpdateAnimatorParameters();
    }

    private void UpdateTransformInterpolation()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 20f);
        
        bool isPositionCloseEnough = Vector3.SqrMagnitude(transform.position - targetPosition) < 0.0001f;
        if (isPositionCloseEnough)
        {
            transform.position = targetPosition;
        }
        
        bool hasValidLookDirection = targetLookDirection != Vector3.zero;
        if (hasValidLookDirection)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetLookDirection), Time.deltaTime * 15f);
        }
    }

private void UpdateAnimatorParameters()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);
        Vector3 localTargetDirection = transform.InverseTransformDirection(targetDirection);
        currentDirection = Vector3.Lerp(currentDirection, localTargetDirection, Time.deltaTime * 10f);       
        
        bool hasAnimator = characterAnimator != null;
        if (hasAnimator)
        {
            characterAnimator.SetFloat(MoveSpeedHash, currentSpeed);
            characterAnimator.SetFloat(HorizontalHash, currentDirection.x);
            characterAnimator.SetFloat(VerticalHash, currentDirection.z);
        }
    }

    
}