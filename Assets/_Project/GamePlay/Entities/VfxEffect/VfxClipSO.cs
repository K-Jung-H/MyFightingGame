using UnityEngine;

[CreateAssetMenu(fileName = "NewVfxClip", menuName = "VFX/Vfx Clip")]
public class VfxClipSO : ScriptableObject
{
    public Sprite[] frames;
    public float frameRate = 30f;
    public float scale = 1f;
    public bool isLooping = false;
    public bool faceCamera = true;

#if UNITY_EDITOR
    public Texture2D sourceSpriteSheet;
#endif
}