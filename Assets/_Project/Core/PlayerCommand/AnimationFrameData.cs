[System.Serializable]
public class AnimationFrameData
{
    public ActionLogicData logicData;
    public bool useRootMotion;
    public bool useRootRotation;
    public RootMotionData[] rootMotionPath;
    public HitboxEvent[] hitboxEvents;
    public HurtboxEvent[] hurtboxEvents;
    public VfxEvent[] vfxEvents;
}