using System;
using System.Text;
using System.IO;
using UnityEngine;

public static class StateDumpUtility
{
    public static unsafe void SaveDumpToFile(string role, GameStateSnapshot snapshot)
    {
        StringBuilder sb = new StringBuilder();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        sb.AppendLine($"[{timestamp}] === GameStateSnapshot Tick: {snapshot.tick} ===");
        sb.AppendLine($"SharedDepthAxis: {FormatFPVector3(snapshot.sharedDepthAxis)}");
        sb.AppendLine($"CurrentTimerFrames: {snapshot.currentTimerFrames}");
        sb.AppendLine($"IsTimerPaused: {snapshot.isTimerPaused}");
        sb.AppendLine($"CurrentPhase: {snapshot.currentPhase}");
        sb.AppendLine($"PhaseDelayTicks: {snapshot.phaseDelayTicks}");
        sb.AppendLine($"SimulationScale: {snapshot.simulationScale.rawValue}");
        sb.AppendLine($"TimeAccumulator: {snapshot.timeAccumulator.rawValue}");
        
        sb.AppendLine($"P1RoundWins: {snapshot.scoreContext.p1RoundWins}");
        sb.AppendLine($"P2RoundWins: {snapshot.scoreContext.p2RoundWins}");
        sb.AppendLine($"CurrentRound: {snapshot.scoreContext.currentRound}");

        sb.AppendLine($"StageActiveWallBitmask: {snapshot.stageActiveWallBitmask}");
        sb.Append("WallDurabilities: [");
        for (int i = 0; i < 32; i++)
        {
            sb.Append($"{snapshot.wallDurabilities[i]}{(i < 31 ? ", " : "")}");
        }
        sb.AppendLine("]");
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

    private static unsafe void DumpPlayerSnapshot(StringBuilder sb, PlayerSnapshot p)
    {
        sb.AppendLine($"Position: {FormatFPVector3(p.position)}");
        sb.AppendLine($"Velocity: {FormatFPVector3(p.velocity)}");
        sb.AppendLine($"DepthAxis: {FormatFPVector3(p.depthAxis)}");
        sb.AppendLine($"CurrentDirection: {FormatFPVector3(p.currentDirection)}");
        sb.AppendLine($"LookDirection: {FormatFPVector3(p.lookDirection)}");

        sb.AppendLine($"IsGrounded: {p.isGrounded}");
        sb.AppendLine($"IsRootMotionActiveThisFrame: {p.isRootMotionActiveThisFrame}");
        sb.AppendLine($"CachedCurrentState: {p.cachedCurrentState}");
        sb.AppendLine($"PreviousStateType: {p.previousStateType}");
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
        sb.AppendLine($"CurrentWallBounceCount: {p.currentWallBounceCount}");
        
        sb.AppendLine($"CurrentHealth: {p.currentHealth}");
        sb.AppendLine($"HitstopCounter: {p.hitstopCounter}");
        sb.AppendLine($"LastImpactFallSpeed: {p.lastImpactFallSpeed.rawValue}");

        sb.AppendLine($"ControllerFrame: {p.controllerFrame}");
        sb.AppendLine($"PreviousRawFlags: {p.previousRawFlags}");
        sb.AppendLine($"AccumulatedHitstopFlags: {p.accumulatedHitstopFlags}");
        sb.AppendLine($"AccumulatedLogicFlags: {p.accumulatedLogicFlags}");

        sb.AppendLine($"ActionController.ComboCount: {p.actionControllerState.comboCount}");
        sb.Append("ActionController.ComboSequence: [");
        for (int i = 0; i < p.actionControllerState.comboCount; i++)
        {
            sb.Append($"{p.actionControllerState.comboSequence[i]}{(i < p.actionControllerState.comboCount - 1 ? ", " : "")}");
        }
        sb.AppendLine("]");

        sb.AppendLine($"ActionController.InputBufferCount: {p.actionControllerState.deterministicInputBuffer.count}");
        sb.AppendLine($"ActionController.InputBufferHead: {p.actionControllerState.deterministicInputBuffer.head}");
        
        sb.AppendLine("ActionController.DeterministicInputBuffer Details: ");
        for (int i = 0; i < p.actionControllerState.deterministicInputBuffer.count; i++)
        {
            int index = (p.actionControllerState.deterministicInputBuffer.head - i + 60) % 60;
            sb.AppendLine($"  [{i}] Frame: {p.actionControllerState.deterministicInputBuffer.frames[index]}, Flags: {p.actionControllerState.deterministicInputBuffer.rawFlags[index]}");
        }

        sb.AppendLine($"Combat.HitGroupCount: {p.combatState.hitGroupCount}");
        sb.Append("Combat.RegisteredHitGroups: [");
        for (int i = 0; i < p.combatState.hitGroupCount; i++)
        {
            sb.Append($"{p.combatState.registeredHitGroups[i]}{(i < p.combatState.hitGroupCount - 1 ? ", " : "")}");
        }
        sb.AppendLine("]");
    }

    private static string FormatFPVector3(FPVector3 v)
    {
        return $"({v.x.rawValue}, {v.y.rawValue}, {v.z.rawValue})";
    }
}