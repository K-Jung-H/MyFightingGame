using System;
using System.Text;
using System.IO;
using UnityEngine;

public static class StateDumpUtility
{
    public static void SaveDumpToFile(string role, GameStateSnapshot snapshot)
    {
        StringBuilder sb = new StringBuilder();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        sb.AppendLine($"[{timestamp}] === GameStateSnapshot Tick: {snapshot.tick} ===");
        sb.AppendLine($"SharedDepthAxis: {FormatFPVector3(snapshot.sharedDepthAxis)}");
        sb.AppendLine();
        
        sb.AppendLine("--- [Player 1 Snapshot] ---");
        DumpPlayerSnapshot(sb, snapshot.p1Snapshot);
        sb.AppendLine();
        
        sb.AppendLine("--- [Player 2 Snapshot] ---");
        DumpPlayerSnapshot(sb, snapshot.p2Snapshot);
        sb.AppendLine("==================================================\n");

        string fileName = $"Log_{role}.txt";
        string filePath = Path.Combine(Application.dataPath, fileName);
        File.AppendAllText(filePath, sb.ToString());
        Debug.Log($"[StateDump] Snapshot appended to: {filePath}");
    }

    private static void DumpPlayerSnapshot(StringBuilder sb, PlayerSnapshot p)
    {
        sb.AppendLine($"Position: {FormatFPVector3(p.position)}");
        sb.AppendLine($"Velocity: {FormatFPVector3(p.velocity)}");
        sb.AppendLine($"DepthAxis: {FormatFPVector3(p.depthAxis)}");
        sb.AppendLine($"CurrentDirection: {FormatFPVector3(p.currentDirection)}");
        sb.AppendLine($"LookDirection: {FormatFPVector3(p.lookDirection)}");
        
        sb.AppendLine($"IsGrounded: {p.isGrounded}");
        sb.AppendLine($"IsRootMotionActiveThisFrame: {p.isRootMotionActiveThisFrame}");
        sb.AppendLine($"LastImpactFallSpeed (Raw): {p.lastImpactFallSpeed.rawValue}");
        
        sb.AppendLine($"CachedCurrentState: {p.cachedCurrentState}");
        sb.AppendLine($"StateFrameCounter: {p.stateFrameCounter}");
        sb.AppendLine($"CurrentActionID: {p.currentActionID}");
        sb.AppendLine($"IsCommandActionTriggered: {p.isCommandActionTriggered}");
        
        sb.AppendLine($"CurrentHurtInfo.Damage: {p.currentHurtInfo.damage}");
        sb.AppendLine($"CurrentHurtInfo.HurtStunFrames: {p.currentHurtInfo.hurtStunFrames}");
        sb.AppendLine($"ScheduledWakeUpType: {p.scheduledWakeUpType}");
        sb.AppendLine($"IsFromRoll: {p.isFromRoll}");
        
        sb.AppendLine($"SideStepDirection (Raw): {p.sideStepDirection.rawValue}");
        sb.AppendLine($"CurrentStunFrames: {p.currentStunFrames}");
        sb.AppendLine($"IsGroundBouncing: {p.isGroundBouncing}");
        
        sb.AppendLine($"CurrentHealth: {p.currentHealth}");
        sb.AppendLine($"HitstopCounter: {p.hitstopCounter}");
    }

    private static string FormatFPVector3(FPVector3 v)
    {
        return $"X:{v.x.rawValue}, Y:{v.y.rawValue}, Z:{v.z.rawValue}";
    }
}