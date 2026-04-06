using System;

public static class StateHashUtility
{
    public static ulong ComputeHash(GameStateSnapshot snapshot)
    {
        ulong hash = 14695981039346656037UL;
        
        hash = CombineHash(hash, (ulong)snapshot.tick);
        hash = CombineHash(hash, ComputeFPVector3Hash(snapshot.sharedDepthAxis));
        hash = CombineHash(hash, ComputePlayerHash(snapshot.p1Snapshot));
        hash = CombineHash(hash, ComputePlayerHash(snapshot.p2Snapshot));
        
        hash = CombineHash(hash, (ulong)snapshot.currentTimerFrames);
        hash = CombineHash(hash, snapshot.isTimerPaused ? 1UL : 0UL);
        hash = CombineHash(hash, (ulong)snapshot.currentPhase);
        hash = CombineHash(hash, (ulong)snapshot.phaseDelayTicks);
        hash = CombineHash(hash, (ulong)snapshot.simulationScale.rawValue);
        hash = CombineHash(hash, (ulong)snapshot.phaseDelayTicks);
        hash = CombineHash(hash, (ulong)snapshot.simulationScale.rawValue);
        hash = CombineHash(hash, (ulong)snapshot.timeAccumulator.rawValue);

        return hash;
    }

    private static ulong ComputePlayerHash(PlayerSnapshot p)
    {
        ulong hash = 14695981039346656037UL;

        hash = CombineHash(hash, ComputeFPVector3Hash(p.position));
        hash = CombineHash(hash, ComputeFPVector3Hash(p.velocity));
        hash = CombineHash(hash, ComputeFPVector3Hash(p.depthAxis));
        hash = CombineHash(hash, ComputeFPVector3Hash(p.currentDirection));
        hash = CombineHash(hash, ComputeFPVector3Hash(p.lookDirection));

        hash = CombineHash(hash, p.isGrounded ? 1UL : 0UL);
        hash = CombineHash(hash, p.isRootMotionActiveThisFrame ? 1UL : 0UL);
        hash = CombineHash(hash, (ulong)p.cachedCurrentState);
        hash = CombineHash(hash, (ulong)p.stateFrameCounter);
        hash = CombineHash(hash, (ulong)p.currentActionID);
        hash = CombineHash(hash, p.isCommandActionTriggered ? 1UL : 0UL);

        hash = CombineHash(hash, (ulong)p.currentHurtInfo.damage);
        hash = CombineHash(hash, (ulong)p.currentHurtInfo.hurtStunFrames);
        hash = CombineHash(hash, (ulong)p.scheduledWakeUpType);
        hash = CombineHash(hash, p.isFromRoll ? 1UL : 0UL);

        hash = CombineHash(hash, (ulong)p.sideStepDirection.rawValue);
        hash = CombineHash(hash, (ulong)p.currentStunFrames);
        hash = CombineHash(hash, p.isGroundBouncing ? 1UL : 0UL);

        hash = CombineHash(hash, (ulong)p.currentHealth);
        hash = CombineHash(hash, (ulong)p.hitstopCounter);

        return hash;
    }

    private static ulong ComputeFPVector3Hash(FPVector3 v)
    {
        ulong hash = 14695981039346656037UL;
        hash = CombineHash(hash, (ulong)v.x.rawValue);
        hash = CombineHash(hash, (ulong)v.y.rawValue);
        hash = CombineHash(hash, (ulong)v.z.rawValue);
        return hash;
    }

    private static ulong CombineHash(ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
        return hash;
    }
}