using UnityEngine;

public class DebugHUD : MonoBehaviour
{
    [SerializeField] private GameLoopManager gameLoopManager;

    private void OnGUI()
    {
        if (gameLoopManager == null) return;

        GUI.Box(new Rect(10, 10, 250, 130), "Server Status (Local Simulation)");
        
        GUI.Label(new Rect(20, 40, 200, 20), $"Current Tick: {gameLoopManager.GetCurrentTick()}");
        
        GUI.Label(new Rect(20, 70, 200, 20), $"P1 State: {gameLoopManager.GetP1State()}");
        GUI.Label(new Rect(20, 85, 200, 20), $"P1 Pos: {gameLoopManager.GetP1Pos()}");

        GUI.Label(new Rect(20, 105, 200, 20), $"P2 State: {gameLoopManager.GetP2State()}");
        GUI.Label(new Rect(20, 120, 200, 20), $"P2 Pos: {gameLoopManager.GetP2Pos()}");
    }
}