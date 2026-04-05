using System;
using System.Text;
using System.IO;
using UnityEngine;

public static class HashTraceUtility
{
    public static void TraceAndDumpHash(string role, GameStateSnapshot snapshot)
    {
        StringBuilder sb = new StringBuilder();
        ulong hash = 14695981039346656037UL;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        sb.AppendLine($"[{timestamp}] [Hash Trace] Tick: {snapshot.tick}");
        
        hash = CombineAndLog(sb, hash, (ulong)snapshot.tick, "Tick");
        hash = TraceFPVector3(sb, hash, snapshot.sharedDepthAxis, "SharedDepthAxis");
        
        hash = CombineAndLog(sb, hash, (ulong)snapshot.currentTimerFrames, "CurrentTimerFrames");
        hash = CombineAndLog(sb, hash, snapshot.isTimerPaused ? 1UL : 0UL, "IsTimerPaused");
        hash = CombineAndLog(sb, hash, (ulong)snapshot.currentPhase, "CurrentPhase");
        hash = CombineAndLog(sb, hash, (ulong)snapshot.phaseDelayTicks, "PhaseDelayTicks");
        
        sb.AppendLine("--- P1 ---");
        hash = TracePlayer(sb, hash, snapshot.p1Snapshot, "P1");
        
        sb.AppendLine("--- P2 ---");
        hash = TracePlayer(sb, hash, snapshot.p2Snapshot, "P2");

        sb.AppendLine($"[Final Hash]: {hash}");
        sb.AppendLine("==================================================\n");

        string fileName = $"Log_{role}.txt";
        string filePath = Path.Combine(Application.dataPath, fileName);
        File.AppendAllText(filePath, sb.ToString());
        Debug.Log($"[HashTrace] Appended to: {filePath}");
    }

    private static ulong TracePlayer(StringBuilder sb, ulong hash, PlayerSnapshot p, string prefix)
    {
        hash = TraceFPVector3(sb, hash, p.position, $"{prefix}.Position");
        hash = TraceFPVector3(sb, hash, p.velocity, $"{prefix}.Velocity");
        hash = TraceFPVector3(sb, hash, p.depthAxis, $"{prefix}.DepthAxis");
        hash = TraceFPVector3(sb, hash, p.currentDirection, $"{prefix}.CurrentDirection");
        hash = TraceFPVector3(sb, hash, p.lookDirection, $"{prefix}.LookDirection");

        hash = CombineAndLog(sb, hash, p.isGrounded ? 1UL : 0UL, $"{prefix}.IsGrounded");
        hash = CombineAndLog(sb, hash, p.isRootMotionActiveThisFrame ? 1UL : 0UL, $"{prefix}.IsRootMotionActive");
        hash = CombineAndLog(sb, hash, (ulong)p.cachedCurrentState, $"{prefix}.CachedCurrentState");
        hash = CombineAndLog(sb, hash, (ulong)p.stateFrameCounter, $"{prefix}.StateFrameCounter");
        hash = CombineAndLog(sb, hash, (ulong)p.currentActionID, $"{prefix}.CurrentActionID");
        hash = CombineAndLog(sb, hash, p.isCommandActionTriggered ? 1UL : 0UL, $"{prefix}.IsCommandActionTriggered");
        hash = CombineAndLog(sb, hash, (ulong)p.currentHurtInfo.damage, $"{prefix}.HurtInfo.Damage");
        hash = CombineAndLog(sb, hash, (ulong)p.currentHurtInfo.hurtStunFrames, $"{prefix}.HurtInfo.StunFrames");
        hash = CombineAndLog(sb, hash, (ulong)p.scheduledWakeUpType, $"{prefix}.ScheduledWakeUpType");
        hash = CombineAndLog(sb, hash, p.isFromRoll ? 1UL : 0UL, $"{prefix}.IsFromRoll");
        hash = CombineAndLog(sb, hash, (ulong)p.sideStepDirection.rawValue, $"{prefix}.SideStepDirection");
        hash = CombineAndLog(sb, hash, (ulong)p.currentStunFrames, $"{prefix}.CurrentStunFrames");
        hash = CombineAndLog(sb, hash, p.isGroundBouncing ? 1UL : 0UL, $"{prefix}.IsGroundBouncing");
        hash = CombineAndLog(sb, hash, (ulong)p.currentHealth, $"{prefix}.CurrentHealth");
        hash = CombineAndLog(sb, hash, (ulong)p.hitstopCounter, $"{prefix}.HitstopCounter");

        return hash;
    }

    private static ulong TraceFPVector3(StringBuilder sb, ulong hash, FPVector3 v, string name)
    {
        hash = CombineAndLog(sb, hash, (ulong)v.x.rawValue, $"{name}.X");
        hash = CombineAndLog(sb, hash, (ulong)v.y.rawValue, $"{name}.Y");
        hash = CombineAndLog(sb, hash, (ulong)v.z.rawValue, $"{name}.Z");
        return hash;
    }

    private static ulong CombineAndLog(StringBuilder sb, ulong hash, ulong value, string name)
    {
        hash ^= value;
        hash *= 1099511628211UL;
        sb.AppendLine($"{name,-30} | Val: {value,-20} | AccumHash: {hash}");
        return hash;
    }
}