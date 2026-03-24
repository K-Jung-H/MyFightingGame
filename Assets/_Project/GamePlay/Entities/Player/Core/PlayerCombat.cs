using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat
{
    private PlayerConfigSO config;
    private int hitstopCounter;
    private HurtInfo currentHurtInfo;
    private HashSet<int> registeredHitGroupIds;
    private CombatEvaluator evaluator;
    private int maxHealth;
    private int currentHealth;

    public event System.Action<int, int> OnHealthChanged;

    public PlayerCombat(PlayerConfigSO playerconfig)
    {
        config = playerconfig;
        registeredHitGroupIds = new HashSet<int>();
        InitializeHealth();
        evaluator = new CombatEvaluator();
        evaluator.Initialize(config);
    }

    public void ExportState(ref PlayerSnapshot snapshot)
    {
        snapshot.currentHealth = this.currentHealth;
        snapshot.hitstopCounter = this.hitstopCounter;
        snapshot.currentHurtInfo = this.currentHurtInfo;

        if (snapshot.combatState.registeredHitGroups == null || snapshot.combatState.registeredHitGroups.Length != 10)
        {
            snapshot.combatState.registeredHitGroups = new int[10];
        }

        int index = 0;
        foreach (int hitId in registeredHitGroupIds)
        {
            if (index >= 10) break;
            snapshot.combatState.registeredHitGroups[index] = hitId;
            index++;
        }
        snapshot.combatState.hitGroupCount = index;
    }

    public void ImportState(PlayerSnapshot snapshot)
    {
        if (this.currentHealth != snapshot.currentHealth)
        {
            this.currentHealth = snapshot.currentHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        this.hitstopCounter = snapshot.hitstopCounter;
        this.currentHurtInfo = snapshot.currentHurtInfo;

        registeredHitGroupIds.Clear();
        
        bool hasValidHitGroups = snapshot.combatState.registeredHitGroups != null;
        if (hasValidHitGroups)
        {
            for (int i = 0; i < snapshot.combatState.hitGroupCount; i++)
            {
                registeredHitGroupIds.Add(snapshot.combatState.registeredHitGroups[i]);
            }
        }
    }

    public void InitializeHealth()
    {
        maxHealth = config.GetMaxHealth();
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool ProcessHitstopTick()
    {
        bool isHitstopActive = hitstopCounter > 0;
        if (isHitstopActive)
        {
            hitstopCounter--;
            return true;
        }
        return false;
    }

    public void ApplyHitstop(int frames)
    {
        hitstopCounter = frames;
    }

    public int GetHitstopCounter() => hitstopCounter;

    public void ClearRegisteredHitGroupIds()
    {
        registeredHitGroupIds.Clear();
    }

    public bool HasAlreadyHit(int hitGroupID)
    {
        return registeredHitGroupIds.Contains(hitGroupID);
    }

    public void RegisterHitGroup(int hitGroupID)
    {
        registeredHitGroupIds.Add(hitGroupID);
    }

    public EvaluationResult ProcessIncomingHit(HitboxEvent hitEvent, PlayerController attacker, PlayerController defender)
    {
        PlayerState_Type currentState = defender.GetStateMachine().GetCurrentState();
        bool isMoving = currentState != PlayerState_Type.Idle && currentState != PlayerState_Type.Crouching;

        if (currentState == PlayerState_Type.Crouching)
        {
            FPVector3 horizontalVelocity = defender.GetPhysics().GetFPVelocity();
            horizontalVelocity.y = new FP64(0);
            FP64 sqrMag = (horizontalVelocity.x * horizontalVelocity.x) + (horizontalVelocity.z * horizontalVelocity.z);
            isMoving = sqrMag.rawValue > 0;
        }

        EvaluationResult result = evaluator.EvaluateHit(hitEvent, currentState, isMoving);

        if (!result.isEvaded)
        {
            ApplyHit(result, attacker, defender);
        }

        return result;
    }

    public void ApplyHit(EvaluationResult result, PlayerController attacker, PlayerController defender)
    {
        PlayerStateMachine stateMachine = defender.GetStateMachine();
        int damage = result.hurtInfo.damage;
        bool isDamageValid = damage > 0;

        if (isDamageValid)
        {
            currentHealth = Mathf.Max(0, currentHealth - damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        PlayerPhysics physics = defender.GetPhysics();
        PlayerActionController actionController = defender.GetActionController();
        PlayerState_Type currentStateType = stateMachine.GetCurrentState();
        HurtInfo hurtData = result.hurtInfo;
        currentHurtInfo = hurtData;
        
        FPVector3 finalPushback = new FPVector3(new FP64(0), new FP64(0), new FP64(0));
        bool isBlocked = result.targetState == PlayerState_Type.StandBlock || result.targetState == PlayerState_Type.CrouchBlock;
        
        if (!isBlocked)
        {
            finalPushback = CalculateWorldPushbackFP(attacker.GetPhysics().GetFPLookDirection(), hurtData.pushbackVector);
        }

        bool isAlreadyInAirHit = currentStateType == PlayerState_Type.AirHit ||
                                 currentStateType == PlayerState_Type.GroundSmash ||
                                 currentStateType == PlayerState_Type.LayingDown ||
                                 currentStateType == PlayerState_Type.WakeUp;

        bool isJuggleBumpNeeded = (!physics.GetIsGrounded() || isAlreadyInAirHit) && finalPushback.y.rawValue < 16384;
        if (isJuggleBumpNeeded)
        {
            finalPushback.y = FP64.FromFloat(0.25f);
        }

        physics.SetFPVelocity(finalPushback);

        PlayerState_Type nextState = result.targetState;

        if (isAlreadyInAirHit)
        {
            nextState = PlayerState_Type.AirHit;
        }

        actionController.ClearComboSequence();
        actionController.ClearAllBuffers();
        stateMachine.TransitionTo(nextState, true);
    }

    private FPVector3 CalculateWorldPushbackFP(FPVector3 lookDirection, FPVector3 localPushback)
    {
        FPVector3 upVector = new FPVector3(new FP64(0), FP64.FromFloat(1f), new FP64(0));
        FPVector3 rightDirection = FPVector3.Cross(upVector, lookDirection);
        
        FPVector3 forwardPush = lookDirection * localPushback.z;
        FPVector3 upPush = upVector * localPushback.y;
        FPVector3 rightPush = rightDirection * localPushback.x;
        
        return forwardPush + upPush + rightPush;
    }

    public HurtInfo GetCurrentHurtInfo() => currentHurtInfo;
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
}