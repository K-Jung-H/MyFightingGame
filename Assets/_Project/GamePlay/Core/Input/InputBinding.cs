using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class InputBinding
{
    [Header("Movement")]
    public Key upKey;
    public Key downKey;
    public Key leftKey;
    public Key rightKey;

    [Header("Attack")]
    public Key lpKey;
    public Key rpKey;
    public Key lkKey;
    public Key rkKey;

    [Header("System")]
    public Key pauseKey;
    public Key selectKey;

    public bool IsValid()
    {
        return upKey != Key.None && downKey != Key.None &&
               leftKey != Key.None && rightKey != Key.None &&
               lpKey != Key.None && rpKey != Key.None &&
               lkKey != Key.None && rkKey != Key.None &&
               pauseKey != Key.None && selectKey != Key.None;
    }

    public static InputBinding GetDefaultP1()
    {
        return new InputBinding
        {
            upKey = Key.UpArrow,
            downKey = Key.DownArrow,
            leftKey = Key.LeftArrow,
            rightKey = Key.RightArrow,
            lpKey = Key.U,
            rpKey = Key.I,
            lkKey = Key.J,
            rkKey = Key.K,
            pauseKey = Key.Escape,
            selectKey = Key.Space
        };
    }

    public static InputBinding GetDefaultP2()
    {
        return new InputBinding
        {
            upKey = Key.W,
            downKey = Key.S,
            leftKey = Key.A,
            rightKey = Key.D,
            lpKey = Key.T,
            rpKey = Key.Y,
            lkKey = Key.G,
            rkKey = Key.H,
            pauseKey = Key.Escape,
            selectKey = Key.Space
        };
    }
}