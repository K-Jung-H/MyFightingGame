using UnityEngine;
using System;

public class GameSimulationCore
{
    private BoundaryPlane[] staticStageGeometry;

    public void Initialize(StageBoundary initialBoundary)
    {
        staticStageGeometry = initialBoundary.Planes;
    }

    public StageBoundary GetRuntimeBoundary(uint bitmask)
    {
        if (staticStageGeometry == null) return new StageBoundary();
        
        BoundaryPlane[] currentPlanes = new BoundaryPlane[staticStageGeometry.Length];
        for (int i = 0; i < staticStageGeometry.Length; i++)
        {
            currentPlanes[i] = staticStageGeometry[i];
            currentPlanes[i].isActive = (bitmask & (1u << i)) != 0; 
        }
        return new StageBoundary { Planes = currentPlanes };
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
                bool p1Cornered = ResolveStageCollision(p1Controller, ref simState);
                bool p2Cornered = ResolveStageCollision(p2Controller, ref simState);

                ResolveAttacks(p1Controller, p2Controller, onHitSpark);
                ResolveAttacks(p2Controller, p1Controller, onHitSpark);
                ResolvePlayerCollision(p1Controller, p2Controller, p1Cornered, p2Cornered);
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
        
        long epsilonRaw = 655;
        
        if (distSqr.rawValue < epsilonRaw)
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

    private bool ResolveStageCollision(PlayerController player, ref SimulationState simState)
    {
        if (staticStageGeometry == null) return false;

        PlayerPhysics physics = player.GetPhysics();
        FPVector3 currentPos = physics.GetFPPosition();
        FPVector3 currentVel = physics.GetFPVelocity();
        FPVector3 totalPushback = new FPVector3(new FP64(0), new FP64(0), new FP64(0));

        bool isCornered = false;

        for (int iter = 0; iter < 2; iter++)
        {
            bool hasCollision = false;
            for (int i = 0; i < staticStageGeometry.Length; i++)
            {
                if ((simState.stageActiveWallBitmask & (1u << i)) == 0) continue;

                FPVector3 normal = staticStageGeometry[i].Normal;
                FP64 distance = staticStageGeometry[i].Distance;
                FPVector3 testPos = currentPos + totalPushback;
                
                FP64 centerDistanceToWall = FPVector3.Dot(testPos, normal) - distance;

                FPVector3 wallDirection = new FPVector3(new FP64(-normal.x.rawValue), new FP64(-normal.y.rawValue), new FP64(-normal.z.rawValue));
                FP64 dynamicRadius = GetDynamicPushBoundary(player, wallDirection);

                if (centerDistanceToWall.rawValue < dynamicRadius.rawValue)
                {
                    FP64 penetration = centerDistanceToWall - dynamicRadius;
                    totalPushback = totalPushback - (normal * penetration);
                    
                    FP64 velDotNormal = FPVector3.Dot(currentVel, normal);
                    if (velDotNormal.rawValue < 0) currentVel = currentVel - (normal * velDotNormal);
                    
                    hasCollision = true;
                    isCornered = true;
                }
            }
            if (!hasCollision) break;
        }

        if (totalPushback.x.rawValue != 0 || totalPushback.y.rawValue != 0 || totalPushback.z.rawValue != 0) physics.ApplyFPPushback(totalPushback);
        physics.SetFPVelocity(currentVel);

        return isCornered;
    }



    private void ResolveAttacks(PlayerController attacker, PlayerController defender, Action<PlayerController, Vector3, EffectType> onHitSpark)
    {
        if (!IsValidAttackAttempt(attacker, out ActionDataSO attackerAction)) return;

        Hurtbox_Type defenderHurtboxType = defender.GetStateMachine().GetCurrentHurtboxType();
        FPCollisionBox[] defenderBoxes = defender.GetConfig().GetFPHurtboxBoxes(defenderHurtboxType);

        if (defenderBoxes == null) return;

        bool isHit = HitboxManager.EvaluateHit(
            attacker.GetFPPosition(),
            attacker.GetFPLookDirection(),
            attackerAction.GetFPHitboxEvents(),
            attacker.GetStateMachine().GetStateFrameCounter(),
            defender.GetFPPosition(),
            defender.GetFPLookDirection(),
            defenderBoxes,
            out FPHitboxEvent fpEvt,
            out FPVector3 fpHitPoint
        );

        if (isHit)
        {
            Vector3 hitPoint = fpHitPoint.ToVector3();
            ProcessSuccessfulHit(attacker, defender, fpEvt, hitPoint, onHitSpark);
        }
    }

    private bool IsValidAttackAttempt(PlayerController attacker, out ActionDataSO actionData)
    {
        actionData = attacker.GetStateMachine().GetCurrentActionData();
        bool isAttacking = attacker.GetStateMachine().GetCurrentState() == PlayerState_Type.Attacking;
        bool hasValidData = actionData != null && actionData.frameData.hitboxEvents != null;

        return isAttacking && hasValidData;
    }

    private void ProcessSuccessfulHit(PlayerController attacker, PlayerController defender, FPHitboxEvent hitEvent, Vector3 hitPoint, Action<PlayerController, Vector3, EffectType> onHitSpark)
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

    private void ResolvePlayerCollision(PlayerController playerOne, PlayerController playerTwo, bool isP1Cornered, bool isP2Cornered)
    {
        FPVector3 p1Pos = playerOne.GetFPPosition();
        FPVector3 p2Pos = playerTwo.GetFPPosition();

        FPVector3 diff = p1Pos - p2Pos;
        diff.y = new FP64(0);
        
        FP64 distanceSqr = (diff.x * diff.x) + (diff.z * diff.z);
        if (distanceSqr.rawValue == 0) return;

        FP64 distance = FP64.Sqrt(distanceSqr);
        
        FPVector3 dirToP2 = new FPVector3(new FP64(-diff.x.rawValue) / distance, new FP64(0), new FP64(-diff.z.rawValue) / distance);
        FPVector3 dirToP1 = new FPVector3(diff.x / distance, new FP64(0), diff.z / distance);

        FP64 p1Radius = GetDynamicPushBoundary(playerOne, dirToP2);
        FP64 p2Radius = GetDynamicPushBoundary(playerTwo, dirToP1);
        
        FP64 requiredDistance = p1Radius + p2Radius;

        if (distance.rawValue < requiredDistance.rawValue)
        {
            FP64 totalPushDist = requiredDistance - distance;
            FPVector3 pushDir = dirToP1;
            
            FP64 w1 = GetPushbackWeight(playerOne.GetStateMachine().GetCurrentState());
            FP64 w2 = GetPushbackWeight(playerTwo.GetStateMachine().GetCurrentState());

            if (isP1Cornered && !isP2Cornered)
            {
                w1 = new FP64(0);
                w2 = new FP64(65536);
            }
            else if (isP2Cornered && !isP1Cornered)
            {
                w1 = new FP64(65536);
                w2 = new FP64(0);
            }

            FP64 totalWeight = w1 + w2;

            long epsilonRaw = 6;
            if (totalWeight.rawValue <= epsilonRaw)
            {
                w1 = new FP64(32768);
                w2 = new FP64(32768);
                totalWeight = new FP64(65536);
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

        long oneFP = 65536;

        if (isSprinting) return new FP64(0);
        if (isRunning) return new FP64(oneFP / 5);
        if (isWalking) return new FP64(oneFP / 2);
        return new FP64(oneFP);
    }

    private FP64 GetDynamicPushBoundary(PlayerController player, FPVector3 directionNormal)
    {
        Hurtbox_Type currentType = player.GetStateMachine().GetCurrentHurtboxType();
        FPCollisionBox[] boxes = player.GetConfig().GetFPHurtboxBoxes(currentType);

        if (boxes == null || boxes.Length == 0) return new FP64(32768); 

        FPVector3 lookDir = player.GetFPLookDirection();
        FP64 maxBoundary = new FP64(0);

        for (int i = 0; i < boxes.Length; i++)
        {
            FPVector3 localPos = boxes[i].localPosition;
            FPVector3 extents = boxes[i].extents;

            if (lookDir.x.rawValue < 0) 
            {
                localPos.x = new FP64(-localPos.x.rawValue);
            }

            FP64 centerProj = FPVector3.Dot(localPos, directionNormal);
            
            FP64 extX = (extents.x.rawValue * directionNormal.x.rawValue < 0) ? new FP64(-(extents.x.rawValue * directionNormal.x.rawValue) >> FP64.fractionalBits) : new FP64((extents.x.rawValue * directionNormal.x.rawValue) >> FP64.fractionalBits);
            FP64 extY = (extents.y.rawValue * directionNormal.y.rawValue < 0) ? new FP64(-(extents.y.rawValue * directionNormal.y.rawValue) >> FP64.fractionalBits) : new FP64((extents.y.rawValue * directionNormal.y.rawValue) >> FP64.fractionalBits);
            FP64 extZ = (extents.z.rawValue * directionNormal.z.rawValue < 0) ? new FP64(-(extents.z.rawValue * directionNormal.z.rawValue) >> FP64.fractionalBits) : new FP64((extents.z.rawValue * directionNormal.z.rawValue) >> FP64.fractionalBits);
            
            FP64 extentsProj = new FP64(extX.rawValue + extY.rawValue + extZ.rawValue);
                               
            FP64 boundary = centerProj + extentsProj;
            
            if (boundary.rawValue > maxBoundary.rawValue)
            {
                maxBoundary = boundary;
            }
        }
        
        if (maxBoundary.rawValue < 0) maxBoundary = new FP64(0);
        return maxBoundary;
    }
}
