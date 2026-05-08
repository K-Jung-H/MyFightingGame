using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private float locomotionBlendTime = 0.1f;
    [SerializeField] private float actionBlendTime = 0.05f; 
    
    private EffectTableSO effectTable;
    private PlayerController controller;
    private StateAnimationMapSO stateAnimMap;
    
    private Vector3 targetPosition;
    private Vector3 targetLookDirection;
    private float targetSpeed;
    private float currentSpeed;
    private PlayerState_Type previousState;
    private ActionDataSO previousActionData;
    private int previousStateFrame = -1;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int LocomotionHash = Animator.StringToHash("Move Blend Tree");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

    public void InitializeRenderer(PlayerController playerController, StateAnimationMapSO stateMap, EffectTableSO fxTable)
    {
        controller = playerController;
        stateAnimMap = stateMap;
        effectTable = fxTable;
        previousState = (PlayerState_Type)(-1);
        previousActionData = null;
        previousStateFrame = -1;
    }

    public void UpdateRenderer(float simulationScale = 1f)
    {
        if (controller == null) return;

        bool isHitstopActive = controller.GetCombat().GetHitstopCounter() > 0;

        if (characterAnimator != null)
        {
            characterAnimator.speed = isHitstopActive ? 0f : simulationScale;
        }

        if (isHitstopActive) return;

        SyncTransformWithLogic();
        EvaluateAndPlayAnimation(simulationScale); 
    }

    public void PlayHitSpark(Vector3 hitPosition, EffectType effectType)
    {
        VfxClipSO resolvedClip = effectTable != null ? effectTable.GetClip(effectType) : null;
        if (resolvedClip != null)
        {
            VfxManager.Instance.SpawnVfxAtPosition(resolvedClip, hitPosition, Quaternion.identity);
        }
    }

    private void Update()
    {
        if (controller == null) return;

        bool isHitstopActive = controller.GetCombat().GetHitstopCounter() > 0;
        if (isHitstopActive) return;

        UpdateTransformInterpolation();
        UpdateAnimatorParameters();
    }

    private void SyncTransformWithLogic()
    {
        targetPosition = controller.GetPhysics().GetPosition();
        targetSpeed = GetSpeedFromState(controller.GetStateMachine().GetCurrentState()); 
        targetLookDirection = controller.GetPhysics().GetLookDirection();
    }

    private void UpdateTransformInterpolation()
    {
        float interpolationFactor = Time.deltaTime * 30f;

        transform.position = Vector3.Lerp(transform.position, targetPosition, interpolationFactor);

        bool isPositionCloseEnough = Vector3.SqrMagnitude(transform.position - targetPosition) < 0.0001f;
        if (isPositionCloseEnough)
        {
            transform.position = targetPosition;
        }

        bool hasValidLookDirection = targetLookDirection != Vector3.zero;
        if (hasValidLookDirection)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetLookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, interpolationFactor);
        }
    }

    private void EvaluateAndPlayAnimation(float currentScale)
    {
        PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
        int stateFrame = controller.GetStateMachine().GetStateFrameCounter();
        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();

        bool isStateChanged = previousState != currentState;
        bool isActionChanged = previousActionData != currentAction;

        bool isStrictSyncState = currentState == PlayerState_Type.Attacking ||
                                 currentState == PlayerState_Type.StandHit ||
                                 currentState == PlayerState_Type.CrouchHit ||
                                 currentState == PlayerState_Type.StandBlock ||
                                 currentState == PlayerState_Type.CrouchBlock ||
                                 currentState == PlayerState_Type.Knockback_Air ||
                                 currentState == PlayerState_Type.Stunning ||
                                 currentState == PlayerState_Type.GroundSmash ||
                                 currentState == PlayerState_Type.LayingDown ||
                                 currentState == PlayerState_Type.WakeUp ||
                                 currentState == PlayerState_Type.Dead ||
                                 currentState == PlayerState_Type.Defeat ||
                                 currentState == PlayerState_Type.Win;

        if (isStrictSyncState)
        {
            int hash = 0;

            if (currentState == PlayerState_Type.Attacking)
            {
                if (currentAction != null && !string.IsNullOrEmpty(currentAction.animationStateName))
                {
                    hash = controller.GetStateMachine().GetAnimationHash(currentAction.animationStateName);
                }
            }
            else if (currentState == PlayerState_Type.Dead)
            {
                PlayerState_Type prevState = controller.GetStateMachine().GetPreviousStateType();
                AnimationClip deadClip = stateAnimMap.GetDeadAnimationClip(prevState);
                if (deadClip != null) hash = controller.GetStateMachine().GetAnimationHash(deadClip.name);
            }
            else
            {
                hash = GetStaticStateHash(currentState);
            }

            if (hash != 0)
            {
                float exactTime = (stateFrame > 0 ? stateFrame - 1 : 0) * (1f / 60f);
                
                bool isNewTransition = isStateChanged || isActionChanged;
                bool isRollbackDesync = false;

                if (!isNewTransition && previousStateFrame != -1)
                {
                    int frameDiff = stateFrame - previousStateFrame;
                    if (frameDiff < 0 || frameDiff > 1)
                    {
                        isRollbackDesync = true;
                    }
                }

                if (isNewTransition)
                {
                    if (currentState == PlayerState_Type.Attacking)
                    {
                        characterAnimator.CrossFadeInFixedTime(hash, actionBlendTime, 0, exactTime);
                    }
                    else
                    {
                        characterAnimator.PlayInFixedTime(hash, 0, exactTime);
                    }
                }
                else if (isRollbackDesync)
                {
                    characterAnimator.PlayInFixedTime(hash, 0, exactTime);
                }
            }
        }
        else if (isStateChanged)
        {
            EvaluateLocomotionTransition(currentState);
        }

        EvaluateVfxEvents(currentScale);

        previousState = currentState;
        previousActionData = currentAction;
        previousStateFrame = stateFrame; 
    }

    private int GetStaticStateHash(PlayerState_Type currentState)
    {
        AnimationClip targetClip = null;
        if (stateAnimMap != null)
        {
            if (currentState == PlayerState_Type.WakeUp)
            {
                WakeUpState wakeUpState = controller.GetStateMachine().GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;
                WakeUp_Type currentWakeUpType = wakeUpState != null ? wakeUpState.GetScheduledWakeUpType() : WakeUp_Type.InPlace;
                targetClip = stateAnimMap.GetWakeUpAnimationClip(currentWakeUpType);
            }
            else if (currentState == PlayerState_Type.LayingDown)
            {
                LayingDownState layState = controller.GetStateMachine().GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
                bool isFromRoll = layState != null && layState.IsFromRoll();
                targetClip = stateAnimMap.GetLayingDownAnimationClip(isFromRoll);
            }
            else if (currentState == PlayerState_Type.StandHit || 
                     currentState == PlayerState_Type.CrouchHit ||
                     currentState == PlayerState_Type.StandBlock ||
                     currentState == PlayerState_Type.CrouchBlock)
            {
                HurtInfo hurtInfo = controller.GetCombat().GetCurrentHurtInfo();
                targetClip = stateAnimMap.GetHurtAnimationClip(currentState, hurtInfo.attackHeight);
            }
            else
            {
                targetClip = stateAnimMap.GetStateAnimationClip(currentState);
            }
        }

        string animName = targetClip != null ? targetClip.name : currentState.ToString();
        return controller.GetStateMachine().GetAnimationHash(animName);
    }

    private void EvaluateLocomotionTransition(PlayerState_Type currentState)
    {
        bool isCurrentLocomotion = currentState == PlayerState_Type.Idle || 
                                   currentState == PlayerState_Type.Walking || 
                                   currentState == PlayerState_Type.Running || 
                                   currentState == PlayerState_Type.Sprinting ||
                                   currentState == PlayerState_Type.SideWalk ||
                                   currentState == PlayerState_Type.SideStep ||
                                   currentState == PlayerState_Type.Crouching;
            
        bool isPreviousAction = previousState == PlayerState_Type.Attacking || 
                                previousState == PlayerState_Type.StandHit || 
                                previousState == PlayerState_Type.CrouchHit ||
                                previousState == PlayerState_Type.StandBlock ||
                                previousState == PlayerState_Type.CrouchBlock ||
                                previousState == PlayerState_Type.Knockback_Air || 
                                previousState == PlayerState_Type.Stunning || 
                                previousState == PlayerState_Type.GroundSmash ||
                                previousState == PlayerState_Type.LayingDown ||
                                previousState == PlayerState_Type.WakeUp ||
                                previousState == PlayerState_Type.Dead ||
                                previousState == PlayerState_Type.Defeat ||
                                previousState == PlayerState_Type.Win ||
                                previousState == PlayerState_Type.SideStep; 

        if (isCurrentLocomotion && isPreviousAction)
        {
            characterAnimator.CrossFadeInFixedTime(LocomotionHash, locomotionBlendTime, 0);
        }
    }

    private void UpdateAnimatorParameters()
    {
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);
        if (currentSpeed < 0.01f) currentSpeed = 0f;

        Vector3 worldVelocity = controller.GetPhysics().GetVelocity();
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

        if (characterAnimator != null)
        {
            characterAnimator.SetFloat(MoveSpeedHash, currentSpeed);
            
            float maxSpeed = controller.GetConfig().walkSpeed; 
            if (maxSpeed > 0)
            {
                characterAnimator.SetFloat(HorizontalHash, localVelocity.x / maxSpeed);
                characterAnimator.SetFloat(VerticalHash, localVelocity.z / maxSpeed);
            }

            PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
            characterAnimator.SetBool(IsCrouchingHash, currentState == PlayerState_Type.Crouching);
        }
    }

    private void EvaluateVfxEvents(float currentScale)
    {
        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();
        if (currentAction == null || currentAction.frameData.vfxEvents == null) return;

        PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
        int currentFrame = controller.GetStateMachine().GetStateFrameCounter();

        if (currentFrame == previousStateFrame && currentState == previousState) 
        {
            return;
        }

        foreach (var vfxEvent in currentAction.frameData.vfxEvents)
        {
            if (currentFrame >= vfxEvent.startFrame && currentFrame <= vfxEvent.endFrame)
            {
                bool isSpawnFrame = vfxEvent.intervalFrames <= 0 
                                    ? currentFrame == vfxEvent.startFrame 
                                    : (currentFrame - vfxEvent.startFrame) % vfxEvent.intervalFrames == 0;

                if (isSpawnFrame)
                {
                    Transform targetBoneTransform = characterAnimator.GetBoneTransform(vfxEvent.targetBone);
                    VfxClipSO resolvedClip = effectTable != null ? effectTable.GetClip(vfxEvent.effectType) : null;
                    
                    if (targetBoneTransform != null && resolvedClip != null)
                    {
                        VfxManager.Instance.SpawnVfx(
                            resolvedClip, 
                            targetBoneTransform, 
                            vfxEvent.localPositionOffset, 
                            vfxEvent.localRotationOffset, 
                            vfxEvent.isAttached,
                            currentScale
                        );
                    }
                }
            }
        }
    }

    private float GetSpeedFromState(PlayerState_Type stateType)
    {
        if (stateType == PlayerState_Type.Walking || stateType == PlayerState_Type.SideWalk || stateType == PlayerState_Type.SideStep) return 1.0f;
        if (stateType == PlayerState_Type.Running) return 2.0f;
        if (stateType == PlayerState_Type.Sprinting) return 3.0f;
        
        if (stateType == PlayerState_Type.Crouching)
        {
            Vector3 currentVelocity = controller.GetPhysics().GetVelocity();
            currentVelocity.y = 0f;
            if (currentVelocity.sqrMagnitude > 0.0001f) return 1.0f;
        }

        return 0.0f;
    }
}