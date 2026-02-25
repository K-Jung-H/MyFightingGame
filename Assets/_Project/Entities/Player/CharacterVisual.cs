using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    private Vector3 targetPosition;
    private float targetSpeed;
    private Vector3 targetDirection;
    private Vector3 targetLookDirection;
    private float currentSpeed;
    private Vector3 currentDirection;
    private PlayerState previousState;
    
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int StunBoolHash = Animator.StringToHash("IsStunned");
    
    private static readonly int LocomotionHash = Animator.StringToHash("Move Blend Tree");

    public void SyncWithLogic(Vector3 logicPosition, PlayerState state, float speed, Vector3 direction, Vector3 lookDirection, bool triggerAction, int customActionHash)
    {
        targetPosition = logicPosition;
        targetSpeed = speed;
        targetDirection = direction;
        targetLookDirection = lookDirection;

        if (triggerAction)
        {
            characterAnimator.CrossFadeInFixedTime(customActionHash, 0.1f, 0);
        }

        if (previousState != state)
        {
            characterAnimator.SetBool(StunBoolHash, state == PlayerState.Stun);

            bool isCurrentLocomotion = state == PlayerState.Idle || state == PlayerState.Walking || state == PlayerState.Running || state == PlayerState.Sprinting;
            bool isPreviousAction = previousState == PlayerState.Attacking || previousState == PlayerState.Stun;

            if (isCurrentLocomotion && isPreviousAction)
            {
                characterAnimator.CrossFadeInFixedTime(LocomotionHash, 0.1f, 0);
            }
        }
        previousState = state;
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 20f);
        if (targetLookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetLookDirection), Time.deltaTime * 15f);
        }
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);
        currentDirection = Vector3.Lerp(currentDirection, targetDirection, Time.deltaTime * 10f);       
        
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (characterAnimator == null) return;
        characterAnimator.SetFloat(MoveSpeedHash, currentSpeed);
        characterAnimator.SetFloat(HorizontalHash, currentDirection.x);
        characterAnimator.SetFloat(VerticalHash, currentDirection.z);
    }
}