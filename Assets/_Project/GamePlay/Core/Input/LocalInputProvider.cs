using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInputProvider
{
    private InputBinding playerOneBinding;
    private InputBinding playerTwoBinding;

    public LocalInputProvider(InputBinding firstBinding, InputBinding secondBinding)
    {
        playerOneBinding = firstBinding;
        playerTwoBinding = secondBinding;
    }

    public PlayerInput GetCurrentInput(int currentFrame, int playerIndex, bool isFacingRight)
    {
        PlayerInput input = new PlayerInput();
        input.frame = currentFrame;

        InputBinding binding = playerIndex == 0 ? playerOneBinding : playerTwoBinding;
        input.flags = PollInput(binding, isFacingRight);

        return input;
    }

    private InputFlags PollInput(InputBinding binding, bool isFacingRight)
    {
        bool isKeyboardValid = Keyboard.current != null;
        if (!isKeyboardValid)
        {
            return InputFlags.None;
        }

        bool isUp = Keyboard.current[binding.upKey].isPressed;
        bool isDown = Keyboard.current[binding.downKey].isPressed;
        bool isPhysicalLeft = Keyboard.current[binding.leftKey].isPressed;
        bool isPhysicalRight = Keyboard.current[binding.rightKey].isPressed;
        
        bool isLP = Keyboard.current[binding.lpKey].isPressed;
        bool isRP = Keyboard.current[binding.rpKey].isPressed;
        bool isLK = Keyboard.current[binding.lkKey].isPressed;
        bool isRK = Keyboard.current[binding.rkKey].isPressed;

        bool isForward = isFacingRight ? isPhysicalRight : isPhysicalLeft;
        bool isBack = isFacingRight ? isPhysicalLeft : isPhysicalRight;

        return PacketManager.CreateFlags(isUp, isDown, isForward, isBack, isLP, isRP, isLK, isRK);
    }
}