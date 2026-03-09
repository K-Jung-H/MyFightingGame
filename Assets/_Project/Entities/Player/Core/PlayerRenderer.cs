using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private float hitBlendTime = 0f;
    [SerializeField] private float commandBlendTime = 0.1f;
    [SerializeField] private float locomotionBlendTime = 0.1f;
    
    private EffectTableSO effectTable;
    private PlayerController controller;
    private StateAnimationMapSO stateAnimMap;
    
    private Vector3 targetPosition;
    private Vector3 targetDirection;
    private Vector3 targetLookDirection;
    private float targetSpeed;
    
    private Vector3 currentDirection;
    private float currentSpeed;
    private PlayerState_Type previousState;

    private bool requiresFrameSync;
    private int syncedAnimHash;
    private int syncedAnimTotalFrames;
    
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
    }

    public void UpdateRenderer()
    {
        bool isControllerNull = controller == null;
        if (isControllerNull) return;

        bool isHitstopActive = controller.GetCombat().GetHitstopCounter() > 0;
        bool hasAnimator = characterAnimator != null;

        if (isHitstopActive && hasAnimator)
        {
            characterAnimator.speed = 0f;
            return;
        }

        SyncTransformWithLogic();
        EvaluateAndPlayAnimation();

        if (hasAnimator)
        {
            if (requiresFrameSync && syncedAnimTotalFrames > 0)
            {
                int currentLogicFrame = controller.GetStateMachine().GetStateFrameCounter();
                float normalizedTime = Mathf.Clamp01((float)currentLogicFrame / syncedAnimTotalFrames);
                characterAnimator.Play(syncedAnimHash, 0, normalizedTime);
                characterAnimator.speed = 0f;
            }
            else
            {
                characterAnimator.speed = 1f;
            }
        }
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

    private void Update()
    {
        bool isControllerNull = controller == null;
        if (isControllerNull) return;

        bool isHitstopActive = controller.GetCombat().GetHitstopCounter() > 0;
        if (isHitstopActive) return;

        UpdateTransformInterpolation();
        UpdateAnimatorParameters();
    }

    private void SyncTransformWithLogic()
    {
        targetPosition = controller.GetPhysics().GetPosition();
        targetSpeed = GetSpeedFromState(controller.GetStateMachine().GetCurrentState()); 
        targetDirection = controller.GetPhysics().GetCurrentDirection();
        targetLookDirection = controller.GetPhysics().GetLookDirection();
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

    private void EvaluateAndPlayAnimation()
    {
        PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
        int stateFrame = controller.GetStateMachine().GetStateFrameCounter();
        
        bool isStateChanged = previousState != currentState;

        bool isHitState = currentState == PlayerState_Type.StandHit || 
                          currentState == PlayerState_Type.CrouchHit ||
                          currentState == PlayerState_Type.StandBlock ||
                          currentState == PlayerState_Type.CrouchBlock ||
                          currentState == PlayerState_Type.AirHit || 
                          currentState == PlayerState_Type.Stunning || 
                          currentState == PlayerState_Type.GroundSmash ||
                          currentState == PlayerState_Type.LayingDown ||
                          currentState == PlayerState_Type.WakeUp;

        if (isHitState && stateFrame == 1)
        {
            PlayHitAnimation(currentState);
        }
        else if (controller.GetStateMachine().CheckAndConsumeCommandAction(out int commandHash))
        {
            requiresFrameSync = false;
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
        AnimationClip targetClip = null;
        bool isStateMapValid = stateAnimMap != null;
        
        bool isWakeUpState = currentState == PlayerState_Type.WakeUp;
        bool isLayingDownState = currentState == PlayerState_Type.LayingDown;
        bool isHurtOrBlockState = currentState == PlayerState_Type.StandHit || 
                                  currentState == PlayerState_Type.CrouchHit ||
                                  currentState == PlayerState_Type.StandBlock ||
                                  currentState == PlayerState_Type.CrouchBlock;

        if (isStateMapValid)
        {
            if (isWakeUpState)
            {
                WakeUpState wakeUpState = controller.GetStateMachine().GetStateObject(PlayerState_Type.WakeUp) as WakeUpState;
                WakeUp_Type currentWakeUpType = wakeUpState != null ? wakeUpState.GetScheduledWakeUpType() : WakeUp_Type.InPlace;
                targetClip = stateAnimMap.GetWakeUpAnimationClip(currentWakeUpType);
            }
            else if (isLayingDownState)
            {
                LayingDownState layState = controller.GetStateMachine().GetStateObject(PlayerState_Type.LayingDown) as LayingDownState;
                bool isFromRoll = layState != null && layState.IsFromRoll();
                targetClip = stateAnimMap.GetLayingDownAnimationClip(isFromRoll);
            }
            else if (isHurtOrBlockState)
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
        int finalHash = controller.GetStateMachine().GetAnimationHash(animName);

        if (isHurtOrBlockState)
        {
            requiresFrameSync = true;
            syncedAnimHash = finalHash;
            syncedAnimTotalFrames = controller.GetCombat().GetCurrentHurtInfo().hurtStunFrames;
            characterAnimator.Play(finalHash, 0, 0f);
        }
        else
        {
            requiresFrameSync = false;
            SafeCrossFade(finalHash, hitBlendTime);
        }
    }

    private void PlayAttackAnimation()
    {
        ActionDataSO actionData = controller.GetStateMachine().GetCurrentActionData();
        bool hasValidActionData = actionData != null;
        int finalHash = 0;
        int totalFrames = 0;

        if (hasValidActionData)
        {
            bool hasValidActionName = !string.IsNullOrEmpty(actionData.animationStateName);
            if (hasValidActionName)
            {
                finalHash = controller.GetStateMachine().GetAnimationHash(actionData.animationStateName);
                
                if (actionData.frameData != null)
                {
                    totalFrames = actionData.frameData.logicData.totalFrames;
                }
            }
        }

        bool isInvalidHash = finalHash == 0 || totalFrames <= 0;
        if (isInvalidHash)
        {
            requiresFrameSync = false;
            return;
        }

        requiresFrameSync = true;
        syncedAnimHash = finalHash;
        syncedAnimTotalFrames = totalFrames;
        characterAnimator.Play(finalHash, 0, 0f);
    }

    private void EvaluateLocomotionTransition(PlayerState_Type currentState)
    {
        requiresFrameSync = false;
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
                                previousState == PlayerState_Type.AirHit || 
                                previousState == PlayerState_Type.Stunning || 
                                previousState == PlayerState_Type.GroundSmash ||
                                previousState == PlayerState_Type.LayingDown ||
                                previousState == PlayerState_Type.WakeUp ||
                                previousState == PlayerState_Type.SideStep; 

        bool shouldTransitionToLocomotion = isCurrentLocomotion && isPreviousAction;
        if (shouldTransitionToLocomotion)
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

        bool hasAnimator = characterAnimator != null;
        if (hasAnimator)
        {
            characterAnimator.SetFloat(MoveSpeedHash, currentSpeed);
            
            float maxSpeed = controller.GetConfig().walkSpeed; 
            if (maxSpeed > 0)
            {
                characterAnimator.SetFloat(HorizontalHash, localVelocity.x / maxSpeed);
                characterAnimator.SetFloat(VerticalHash, localVelocity.z / maxSpeed);
            }

            PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
            bool isCurrentlyCrouching = currentState == PlayerState_Type.Crouching;
            characterAnimator.SetBool(IsCrouchingHash, isCurrentlyCrouching);
        }
    }

    private void EvaluateVfxEvents()
    {
        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();
        bool isActionInvalid = currentAction == null || currentAction.frameData.vfxEvents == null;
        if (isActionInvalid) return;

        int currentFrame = controller.GetStateMachine().GetStateFrameCounter();

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

    private void SafeCrossFade(int hash, float blendTime)
    {
        bool hasState = characterAnimator.HasState(0, hash);

        if (hasState)
        {
            characterAnimator.CrossFadeInFixedTime(hash, blendTime, 0);
        }
    }

    private float GetSpeedFromState(PlayerState_Type stateType)
    {
        bool isWalking = stateType == PlayerState_Type.Walking;
        bool isRunning = stateType == PlayerState_Type.Running;
        bool isSprinting = stateType == PlayerState_Type.Sprinting;
        bool isSideWalking = stateType == PlayerState_Type.SideWalk || stateType == PlayerState_Type.SideStep;
        bool isCrouching = stateType == PlayerState_Type.Crouching;

        if (isWalking || isSideWalking) return 1.0f;
        if (isRunning) return 2.0f;
        if (isSprinting) return 3.0f;
        
        if (isCrouching)
        {
            Vector3 currentVelocity = controller.GetPhysics().GetVelocity();
            currentVelocity.y = 0f;
            
            bool hasCrouchMovement = currentVelocity.sqrMagnitude > 0.0001f;
            if (hasCrouchMovement)
            {
                return 1.0f;
            }
        }

        return 0.0f;
    }
}