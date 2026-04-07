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

    public PlayerInput GetCurrentInput(int currentFrame, int playerIndex, bool isFacingRight, bool isCameraFlipped)
    {
        PlayerInput input = new PlayerInput();
        input.frame = currentFrame;

        InputBinding binding = playerIndex == 0 ? playerOneBinding : playerTwoBinding;
        input.flags = PollInput(binding, isFacingRight, isCameraFlipped);

        return input;
    }

    private InputFlags PollInput(InputBinding binding, bool isFacingRight, bool isCameraFlipped)
    {
        bool isKeyboardValid = Keyboard.current != null;
        if (!isKeyboardValid)
        {
            return InputFlags.None;
        }

        bool isPhysicalUp = Keyboard.current[binding.upKey].isPressed;
        bool isPhysicalDown = Keyboard.current[binding.downKey].isPressed;
        bool isPhysicalLeft = Keyboard.current[binding.leftKey].isPressed;
        bool isPhysicalRight = Keyboard.current[binding.rightKey].isPressed;
        
        bool isLP = Keyboard.current[binding.lpKey].isPressed;
        bool isRP = Keyboard.current[binding.rpKey].isPressed;
        bool isLK = Keyboard.current[binding.lkKey].isPressed;
        bool isRK = Keyboard.current[binding.rkKey].isPressed;

        bool isForward = isFacingRight ? isPhysicalRight : isPhysicalLeft;
        bool isBack = isFacingRight ? isPhysicalLeft : isPhysicalRight;

        bool isUp = isPhysicalUp;
        bool isDown = isPhysicalDown;

        return PacketManager.CreateFlags(isUp, isDown, isForward, isBack, isLP, isRP, isLK, isRK);
    }
}