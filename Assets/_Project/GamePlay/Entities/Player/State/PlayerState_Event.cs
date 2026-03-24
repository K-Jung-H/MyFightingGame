public class DeadState : PlayerStateBase
{
    public DeadState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Dead;

    public override void UpdateTick(PlayerInput input)
    {
    }
}

public class DefeatState : PlayerStateBase
{
    public DefeatState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Defeat;

    public override void UpdateTick(PlayerInput input)
    {
    }
}

public class WinState : PlayerStateBase
{
    public WinState(PlayerStateMachine sm, PlayerConfigSO cfg) : base(sm, cfg) { }

    public override PlayerState_Type GetStateType() => PlayerState_Type.Win;

    public override void UpdateTick(PlayerInput input)
    {
    }
}