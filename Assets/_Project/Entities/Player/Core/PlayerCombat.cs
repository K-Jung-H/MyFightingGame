using UnityEngine;
using System.Collections.Generic;

public class PlayerCombat
{
    private int hitstopCounter;
    private HurtInfo currentHurtInfo;
    private HashSet<int> registeredHitGroupIds;
    private CombatEvaluator evaluator;

    public PlayerCombat(PlayerConfigSO config)
    {
        registeredHitGroupIds = new HashSet<int>();
        evaluator = new CombatEvaluator();
        evaluator.Initialize(config);
    }

    public bool ProcessHitstopTick()
    {
        bool isHitstopActive = hitstopCounter > 0;
        if (isHitstopActive)
        {
            hitstopCounter--;
            return true;
        }
        return false;
    }

    public void ApplyHitstop(int frames)
    {
        hitstopCounter = frames;
    }

    public int GetHitstopCounter() => hitstopCounter;

    public void ClearRegisteredHitGroupIds()
    {
        registeredHitGroupIds.Clear();
    }

    public bool HasAlreadyHit(int hitGroupID)
    {
        return registeredHitGroupIds.Contains(hitGroupID);
    }

    public void RegisterHitGroup(int hitGroupID)
    {
        registeredHitGroupIds.Add(hitGroupID);
    }

    public EvaluationResult ProcessIncomingHit(HitboxEvent hitEvent, PlayerController controller)
    {
        PlayerState_Type currentState = controller.GetStateMachine().GetCurrentState();
        
        bool isMoving = currentState != PlayerState_Type.Idle && currentState != PlayerState_Type.Crouching;
        
        if (currentState == PlayerState_Type.Crouching)
        {
            Vector3 horizontalVelocity = controller.GetPhysics().GetVelocity();
            horizontalVelocity.y = 0f;
            isMoving = horizontalVelocity.sqrMagnitude > 0.0001f;
        }

        EvaluationResult result = evaluator.EvaluateHit(hitEvent, currentState, isMoving);

        if (!result.isEvaded)
        {
            ApplyHitstop(result.feedbackData.hitstopFrames);
            ApplyHit(result, controller);
        }

        return result;
    }

    public void ApplyHit(EvaluationResult result, PlayerController controller)
    {
        PlayerStateMachine stateMachine = controller.GetStateMachine();
        PlayerPhysics physics = controller.GetPhysics();
        PlayerActionController actionController = controller.GetActionController();

        PlayerState_Type currentStateType = stateMachine.GetCurrentState();
        HurtInfo hurtData = result.hurtInfo;
        currentHurtInfo = hurtData;
        Vector3 finalPushback = hurtData.pushbackVector;

        bool isAlreadyInAirHit = currentStateType == PlayerState_Type.AirHit || 
                                 currentStateType == PlayerState_Type.GroundSmash ||
                                 currentStateType == PlayerState_Type.LayingDown ||
                                 currentStateType == PlayerState_Type.WakeUp;

        bool isJuggleBumpNeeded = (!physics.GetIsGrounded() || isAlreadyInAirHit) && finalPushback.y < 0.25f;
        if (isJuggleBumpNeeded)
        {
            finalPushback.y = 0.25f; 
        }

        physics.SetVelocity(finalPushback);

        PlayerState_Type nextState = result.targetState;

        if (isAlreadyInAirHit)
        {
            nextState = PlayerState_Type.AirHit;
        }

        actionController.ClearComboSequence();
        actionController.ClearAllBuffers();
        stateMachine.TransitionTo(nextState, true);
    }

    public HurtInfo GetCurrentHurtInfo() => currentHurtInfo;
}