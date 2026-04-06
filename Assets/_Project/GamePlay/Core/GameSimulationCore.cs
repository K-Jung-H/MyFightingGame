using UnityEngine;
using System;

public class GameSimulationCore
{
    private FP64 fpCollisionMinDistance;
    private FP64 fpCollisionMinDistanceSqr;

    public void Initialize(float minCollisionDistance)
    {
        fpCollisionMinDistance = FP64.FromFloat(minCollisionDistance);
        fpCollisionMinDistanceSqr = fpCollisionMinDistance * fpCollisionMinDistance;
    }

    public void SimulateFrame(PlayerController p1Controller, PlayerController p2Controller, PlayerInput p1Input, PlayerInput p2Input, ref SimulationState simState, Action<PlayerController, Vector3, EffectType> onHitSpark)
    {
        if (p1Controller != null && p2Controller != null)
        {
            if (simState.isLogicStep)
            {
                UpdateSharedDepthAxis(p1Controller, p2Controller, ref simState.sharedDepthAxis);
            }
            
            p1Controller.UpdateTick(p1Input, simState.isLogicStep);
            p2Controller.UpdateTick(p2Input, simState.isLogicStep);

            if (simState.isLogicStep)
            {
                ResolveAttacks(p1Controller, p2Controller, onHitSpark);
                ResolveAttacks(p2Controller, p1Controller, onHitSpark);
                ResolvePlayerCollision(p1Controller, p2Controller);
            }
        }
    }

    private void UpdateSharedDepthAxis(PlayerController p1, PlayerController p2, ref FPVector3 sharedDepthAxis)
    {
        FPVector3 p1LogicalPos = p1.GetFPPosition();
        FPVector3 p2LogicalPos = p2.GetFPPosition();

        FPVector3 diffPos = p2LogicalPos - p1LogicalPos;
        diffPos.y = new FP64(0);

        FP64 distSqr = (diffPos.x * diffPos.x) + (diffPos.z * diffPos.z);
        FP64 epsilonSqr = FP64.FromFloat(0.0001f);
        
        if (distSqr.rawValue < epsilonSqr.rawValue)
        {
            p1.GetPhysics().SetFPDepthAxis(sharedDepthAxis);
            p2.GetPhysics().SetFPDepthAxis(sharedDepthAxis);
            return;
        }

        diffPos = diffPos.Normalized();

        FPVector3 normal1 = new FPVector3(new FP64(-diffPos.z.rawValue), new FP64(0), new FP64(diffPos.x.rawValue));
        FPVector3 normal2 = new FPVector3(new FP64(diffPos.z.rawValue), new FP64(0), new FP64(-diffPos.x.rawValue));

        FP64 dot1 = FPVector3.Dot(normal1, sharedDepthAxis);
        FP64 dot2 = FPVector3.Dot(normal2, sharedDepthAxis);

        bool isNormal1Closer = dot1.rawValue > dot2.rawValue;
        if (isNormal1Closer)
        {
            sharedDepthAxis = normal1;
        }
        else
        {
            sharedDepthAxis = normal2;
        }

        p1.GetPhysics().SetFPDepthAxis(sharedDepthAxis);
        p2.GetPhysics().SetFPDepthAxis(sharedDepthAxis);
    }

    private void ResolveAttacks(PlayerController attacker, PlayerController defender, Action<PlayerController, Vector3, EffectType> onHitSpark)
    {
        if (!IsValidAttackAttempt(attacker, out ActionDataSO attackerAction)) return;

        CollisionBox[] defenderBoxes = defender.GetConfig().GetHurtboxBoxes(Hurtbox_Type.Standing);

        bool isHit = HitboxManager.EvaluateHit(
            attacker.GetFPPosition(),
            attacker.GetFPLookDirection(),
            attackerAction.frameData.hitboxEvents,
            attacker.GetStateMachine().GetStateFrameCounter(),
            defender.GetFPPosition(),
            defender.GetFPLookDirection(),
            defenderBoxes,
            out HitboxEvent hitEvent,
            out FPVector3 fpHitPoint,
            out string debugReason
        );

        if (isHit)
        {
            Vector3 hitPoint = fpHitPoint.ToVector3();
            ProcessSuccessfulHit(attacker, defender, hitEvent, hitPoint, onHitSpark);
        }
    }

    private bool IsValidAttackAttempt(PlayerController attacker, out ActionDataSO actionData)
    {
        actionData = attacker.GetStateMachine().GetCurrentActionData();
        bool isAttacking = attacker.GetStateMachine().GetCurrentState() == PlayerState_Type.Attacking;
        bool hasValidData = actionData != null && actionData.frameData.hitboxEvents != null;

        return isAttacking && hasValidData;
    }

    private void ProcessSuccessfulHit(PlayerController attacker, PlayerController defender, HitboxEvent hitEvent, Vector3 hitPoint, Action<PlayerController, Vector3, EffectType> onHitSpark)
    {
        bool isAlreadyHit = attacker.GetCombat().HasAlreadyHit(hitEvent.hitGroupID);
        if (isAlreadyHit) return;

        attacker.GetCombat().RegisterHitGroup(hitEvent.hitGroupID);

        EvaluationResult hitResult = defender.GetCombat().ProcessIncomingHit(hitEvent, attacker, defender);

        bool isHitEvaded = hitResult.isEvaded;
        if (isHitEvaded) return;

        int hitstopFrames = hitResult.feedbackData.hitstopFrames;
        if (hitstopFrames > 0)
        {
            attacker.GetCombat().ApplyHitstop(hitstopFrames);
            defender.GetCombat().ApplyHitstop(hitstopFrames);
        }

        bool isAttackBlocked = hitResult.targetState == PlayerState_Type.StandBlock || hitResult.targetState == PlayerState_Type.CrouchBlock;
        if (!isAttackBlocked && onHitSpark != null)
        {
            onHitSpark.Invoke(defender, hitPoint, EffectType.Hit);
        }
    }

    private void ResolvePlayerCollision(PlayerController playerOne, PlayerController playerTwo)
    {
        FPVector3 p1Pos = playerOne.GetFPPosition();
        FPVector3 p2Pos = playerTwo.GetFPPosition();

        FPVector3 diff = p1Pos - p2Pos;
        diff.y = new FP64(0);
        
        FP64 distanceSqr = (diff.x * diff.x) + (diff.z * diff.z);

        bool isOverlapping = distanceSqr.rawValue < fpCollisionMinDistanceSqr.rawValue && distanceSqr.rawValue > 0;
        if (isOverlapping)
        {
            FP64 distance = FP64.Sqrt(distanceSqr);
            FP64 totalPushDist = fpCollisionMinDistance - distance;
            
            FPVector3 pushDir = new FPVector3(
                diff.x / distance,
                new FP64(0),
                diff.z / distance
            );

            PlayerState_Type p1State = playerOne.GetStateMachine().GetCurrentState();
            PlayerState_Type p2State = playerTwo.GetStateMachine().GetCurrentState();

            FP64 w1 = GetPushbackWeight(p1State);
            FP64 w2 = GetPushbackWeight(p2State);
            FP64 totalWeight = w1 + w2;

            bool isWeightTooSmall = totalWeight.rawValue <= FP64.FromFloat(0.0001f).rawValue;
            if (isWeightTooSmall)
            {
                w1 = FP64.FromFloat(0.5f);
                w2 = FP64.FromFloat(0.5f);
                totalWeight = FP64.FromFloat(1.0f);
            }

            FP64 p1Ratio = w1 / totalWeight;
            FP64 p2Ratio = w2 / totalWeight;

            playerOne.GetPhysics().ApplyFPPushback(pushDir * (totalPushDist * p1Ratio));
            
            FPVector3 negativePushDir = new FPVector3(new FP64(-pushDir.x.rawValue), new FP64(0), new FP64(-pushDir.z.rawValue));
            playerTwo.GetPhysics().ApplyFPPushback(negativePushDir * (totalPushDist * p2Ratio));
        }
    }

    private FP64 GetPushbackWeight(PlayerState_Type state)
    {
        bool isSprinting = state == PlayerState_Type.Sprinting;
        bool isRunning = state == PlayerState_Type.Running;
        bool isWalking = state == PlayerState_Type.Walking;

        if (isSprinting) return new FP64(0);
        if (isRunning) return FP64.FromFloat(0.2f);
        if (isWalking) return FP64.FromFloat(0.5f);
        return FP64.FromFloat(1.0f);
    }
}