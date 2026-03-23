using UnityEngine;
using System;
using System.Collections.Generic;

public class VfxManager : MonoBehaviour
{
    private Dictionary<VfxClipSO, Queue<VfxObject>> pools;

    public static VfxManager Instance { get; private set; }

    private void Awake()
    {
        bool isInstanceNull = Instance == null;
        if (isInstanceNull)
        {
            Instance = this;
            pools = new Dictionary<VfxClipSO, Queue<VfxObject>>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SpawnVfx(VfxClipSO clip, Transform targetBone, Vector3 offset, Quaternion rotOffset, bool isAttached)
    {
        bool isClipInvalid = clip == null;
        if (isClipInvalid) return;

        VfxObject vfxObj = GetOrCreateVfx(clip);
        vfxObj.PlayAttached(clip, targetBone, offset, rotOffset, isAttached);
    }

    public void SpawnVfxAtPosition(VfxClipSO clip, Vector3 position, Quaternion rotation)
    {
        bool isClipInvalid = clip == null;
        if (isClipInvalid) return;

        VfxObject vfxObj = GetOrCreateVfx(clip);
        vfxObj.PlayAtPosition(clip, position, rotation);
    }

    private VfxObject GetOrCreateVfx(VfxClipSO clip)
    {
        bool isPoolMissing = !pools.ContainsKey(clip);
        if (isPoolMissing)
        {
            pools.Add(clip, new Queue<VfxObject>());
        }

        bool hasAvailableObject = pools[clip].Count > 0;
        if (hasAvailableObject)
        {
            return pools[clip].Dequeue();
        }

        GameObject newObj = new GameObject("VfxObject");
        newObj.transform.SetParent(transform);
        VfxObject newVfx = newObj.AddComponent<VfxObject>();
        newVfx.OnPlaybackFinished += ReturnToPool;
        return newVfx;
    }

    private void ReturnToPool(VfxObject vfxObj, VfxClipSO clip)
    {
        bool isPoolExisting = pools.ContainsKey(clip);
        if (isPoolExisting)
        {
            pools[clip].Enqueue(vfxObj);
        }
        else
        {
            Destroy(vfxObj.gameObject);
        }
    }
}