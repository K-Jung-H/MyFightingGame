using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private float locomotionBlendTime = 0.1f;
    
    private EffectTableSO effectTable;
    private PlayerController controller;
    private StateAnimationMapSO stateAnimMap;
    
    private Vector3 targetPosition;
    private Vector3 targetLookDirection;
    private float targetSpeed;
    private float currentSpeed;
    private PlayerState_Type previousState;
    private ActionDataSO previousActionData;

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
    }

    public void UpdateRenderer()
    {
        bool isControllerNull = controller == null;
        if (isControllerNull) return;

        bool isHitstopActive = controller.GetCombat().GetHitstopCounter() > 0;
        bool hasAnimator = characterAnimator != null;

        if (hasAnimator)
        {
            characterAnimator.speed = isHitstopActive ? 0f : 1f;
        }

        if (isHitstopActive) return;

        SyncTransformWithLogic();
        EvaluateAndPlayAnimation();
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

    private void EvaluateAndPlayAnimation()
    {
        PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
        int stateFrame = controller.GetStateMachine().GetStateFrameCounter();
        ActionDataSO currentAction = controller.GetStateMachine().GetCurrentActionData();
        
        bool isStateChanged = previousState != currentState;
        bool isActionChanged = previousActionData != currentAction;

        bool isHitState = currentState == PlayerState_Type.StandHit || 
                            currentState == PlayerState_Type.CrouchHit ||
                            currentState == PlayerState_Type.StandBlock ||
                            currentState == PlayerState_Type.CrouchBlock ||
                            currentState == PlayerState_Type.AirHit || 
                            currentState == PlayerState_Type.Stunning || 
                            currentState == PlayerState_Type.GroundSmash ||
                            currentState == PlayerState_Type.LayingDown ||
                            currentState == PlayerState_Type.WakeUp;

        bool isEndState = currentState == PlayerState_Type.Dead || currentState == PlayerState_Type.Win;

        if (currentState == PlayerState_Type.Attacking)
        {
            if (isStateChanged || isActionChanged)
            {
                int hash = 0;
                bool hasActionName = currentAction != null && !string.IsNullOrEmpty(currentAction.animationStateName);
                if (hasActionName)
                {
                    hash = controller.GetStateMachine().GetAnimationHash(currentAction.animationStateName);
                }
                
                bool isHashValid = hash != 0;
                if (isHashValid)
                {
                    float exactTime = (stateFrame > 0 ? stateFrame - 1 : 0) * (1f / 60f);
                    characterAnimator.PlayInFixedTime(hash, 0, exactTime);
                }
            }
        }
        else if (isHitState || isEndState)
        {
            if (isStateChanged)
            {
                int hash = GetStaticStateHash(currentState);
                bool isHashValid = hash != 0;
                if (isHashValid)
                {
                    float exactTime = (stateFrame > 0 ? stateFrame - 1 : 0) * (1f / 60f);
                    characterAnimator.PlayInFixedTime(hash, 0, exactTime);
                }
            }
        }
        else if (isStateChanged)
        {
            EvaluateLocomotionTransition(currentState);
        }

        EvaluateVfxEvents();
        
        previousState = currentState;
        previousActionData = currentAction;
    }

    private int GetStaticStateHash(PlayerState_Type currentState)
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