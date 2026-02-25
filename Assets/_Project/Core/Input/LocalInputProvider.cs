using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInputProvider
{
    private InputFlags accumulatedFlagsPlayerOne;
    private InputFlags accumulatedFlagsPlayerTwo;

    public void AccumulateInputFlags()
    {
        accumulatedFlagsPlayerOne |= PollInput(0);
        accumulatedFlagsPlayerTwo |= PollInput(1);
    }

    public PlayerInput GetCurrentInput(int currentFrame, int playerIndex)
    {
        PlayerInput input = new PlayerInput();
        input.frame = currentFrame;

        if (playerIndex == 0)
        {
            input.flags = accumulatedFlagsPlayerOne;
            accumulatedFlagsPlayerOne = InputFlags.None;
        }
        else
        {
            input.flags = accumulatedFlagsPlayerTwo;
            accumulatedFlagsPlayerTwo = InputFlags.None;
        }

        return input;
    }

    private InputFlags PollInput(int playerIndex)
    {
        bool isUp = false;
        bool isDown = false;
        bool isLeft = false;
        bool isRight = false;
        bool isLight = false;
        bool isHeavy = false;

        if (Keyboard.current != null)
        {
            if (playerIndex == 0)
            {
                isUp = Keyboard.current.upArrowKey.isPressed;
                isDown = Keyboard.current.downArrowKey.isPressed;
                isLeft = Keyboard.current.leftArrowKey.isPressed;
                isRight = Keyboard.current.rightArrowKey.isPressed;
                isLight = Keyboard.current.zKey.isPressed;
                isHeavy = Keyboard.current.xKey.isPressed;
            }
            else
            {
                isUp = Keyboard.current.wKey.isPressed;
                isDown = Keyboard.current.sKey.isPressed;
                isLeft = Keyboard.current.aKey.isPressed;
                isRight = Keyboard.current.dKey.isPressed;
                isLight = Keyboard.current.vKey.isPressed;
                isHeavy = Keyboard.current.bKey.isPressed;
            }
        }

        return PacketManager.CreateFlags(isUp, isDown, isLeft, isRight, isLight, isHeavy);
    }
}