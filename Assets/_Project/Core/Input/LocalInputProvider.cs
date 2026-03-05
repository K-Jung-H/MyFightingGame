using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInputProvider
{
    private InputFlags accumulatedFlagsPlayerOne;
    private InputFlags accumulatedFlagsPlayerTwo;
    private InputBinding playerOneBinding;
    private InputBinding playerTwoBinding;

    public LocalInputProvider(InputBinding firstBinding, InputBinding secondBinding)
    {
        playerOneBinding = firstBinding;
        playerTwoBinding = secondBinding;
    }

    public void AccumulateInputFlags(bool isPlayerOneFacingRight, bool isPlayerTwoFacingRight)
    {
        accumulatedFlagsPlayerOne |= PollInput(playerOneBinding, isPlayerOneFacingRight);
        accumulatedFlagsPlayerTwo |= PollInput(playerTwoBinding, isPlayerTwoFacingRight);
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

    private InputFlags PollInput(InputBinding binding, bool isFacingRight)
    {
        bool isUp = false;
        bool isDown = false;
        bool isPhysicalLeft = false;
        bool isPhysicalRight = false;
        bool isLP = false;
        bool isRP = false;
        bool isLK = false;
        bool isRK = false;

        if (Keyboard.current != null)
        {
            isUp = Keyboard.current[binding.upKey].isPressed;
            isDown = Keyboard.current[binding.downKey].isPressed;
            isPhysicalLeft = Keyboard.current[binding.leftKey].isPressed;
            isPhysicalRight = Keyboard.current[binding.rightKey].isPressed;
            
            isLP = Keyboard.current[binding.lpKey].isPressed;
            isRP = Keyboard.current[binding.rpKey].isPressed;
            isLK = Keyboard.current[binding.lkKey].isPressed;
            isRK = Keyboard.current[binding.rkKey].isPressed;
        }

        bool isForward = isFacingRight ? isPhysicalRight : isPhysicalLeft;
        bool isBack = isFacingRight ? isPhysicalLeft : isPhysicalRight;

        return PacketManager.CreateFlags(isUp, isDown, isForward, isBack, isLP, isRP, isLK, isRK);
    }
}