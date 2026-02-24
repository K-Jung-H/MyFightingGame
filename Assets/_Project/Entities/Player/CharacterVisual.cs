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
    private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");
    private static readonly int StunBoolHash = Animator.StringToHash("IsStunned");

    public void SyncWithLogic(Vector3 logicPosition, PlayerState state, float speed, Vector3 direction, Vector3 lookDirection)
    {
        targetPosition = logicPosition;
        targetSpeed = speed;
        targetDirection = direction;
        targetLookDirection = lookDirection;

        if (previousState != state)
        {
            if (state == PlayerState.Attacking) characterAnimator.SetTrigger(AttackTriggerHash);
            characterAnimator.SetBool(StunBoolHash, state == PlayerState.Stun);
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