using UnityEngine;
using System.Collections.Generic;

public class PlayerCombat
{
    private int hitstopCounter;
    private HurtInfo currentHurtInfo;
    private HashSet<int> registeredHitGroupIds;

    public PlayerCombat()
    {
        registeredHitGroupIds = new HashSet<int>();
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

    public void ApplyHit(HurtInfo hurtData, PlayerController controller)
    {
        PlayerStateMachine stateMachine = controller.GetStateMachine();
        PlayerPhysics physics = controller.GetPhysics();
        PlayerActionController actionController = controller.GetActionController();

        PlayerState_Type currentStateType = stateMachine.GetCurrentState();
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

        PlayerState_Type nextState = PlayerState_Type.StandHit;

        if (isAlreadyInAirHit)
        {
            nextState = PlayerState_Type.AirHit;
        }
        else
        {
            switch (hurtData.targetHurtState)
            {
                case HurtState_Type.StandHit:
                case HurtState_Type.GuardHit:
                    nextState = PlayerState_Type.StandHit;
                    break;
                case HurtState_Type.AirHit:
                    nextState = PlayerState_Type.AirHit;
                    break;
                case HurtState_Type.KnockDown:
                case HurtState_Type.GroundHit:
                    nextState = PlayerState_Type.Stunning;
                    break;
            }
        }

        actionController.ClearComboSequence();
        actionController.ClearAllBuffers();
        stateMachine.TransitionTo(nextState, true);
    }

    public HurtInfo GetCurrentHurtInfo() => currentHurtInfo;
}