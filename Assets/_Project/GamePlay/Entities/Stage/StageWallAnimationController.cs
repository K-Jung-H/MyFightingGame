using UnityEngine;
using System.Collections.Generic;

public class DebrisGroup
{
    public GameObject rootObject; 
    public Rigidbody[] pieces;
    public Vector3[] initialPositions; 
    public Quaternion[] initialRotations;
}

public class StageWallAnimationController : MonoBehaviour
{
    public GameObject[] wallObjects;
    private DebrisGroup[] preSpawnedDebris;

public void PreWarmDebris()
    {
        if (wallObjects == null) return;
        
        preSpawnedDebris = new DebrisGroup[wallObjects.Length];

        int debrisLayer = LayerMask.NameToLayer("VisualDebris");
        if (debrisLayer == -1)
        {
            Debug.LogWarning("[StageWallAnimationController] 'VisualDebris' 레이어가 없습니다. Default(0) 레이어를 사용합니다.");
            debrisLayer = 0;
        }

        for (int i = 0; i < wallObjects.Length; i++)
        {
            if (wallObjects[i] == null) continue;

            GameObject cloneRoot = Instantiate(wallObjects[i], wallObjects[i].transform.position, wallObjects[i].transform.rotation);
            cloneRoot.name = wallObjects[i].name + "_Debris";
            cloneRoot.SetActive(false);

            MeshRenderer[] renderers = cloneRoot.GetComponentsInChildren<MeshRenderer>(true);
            List<Rigidbody> rbs = new List<Rigidbody>();

            foreach (MeshRenderer renderer in renderers)
            {
                GameObject piece = renderer.gameObject;
                piece.transform.SetParent(cloneRoot.transform, true);

                if (piece.GetComponent<Collider>() == null)
                {
                    piece.AddComponent<BoxCollider>();
                }
                
                Rigidbody rb = piece.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = piece.AddComponent<Rigidbody>();
                }

                piece.layer = debrisLayer; 
                
                rbs.Add(rb);
            }

            preSpawnedDebris[i] = new DebrisGroup 
            {
                rootObject = cloneRoot,
                pieces = rbs.ToArray(),
                initialPositions = new Vector3[rbs.Count],
                initialRotations = new Quaternion[rbs.Count]
            };

            for (int j = 0; j < rbs.Count; j++)
            {
                preSpawnedDebris[i].initialPositions[j] = rbs[j].transform.localPosition;
                preSpawnedDebris[i].initialRotations[j] = rbs[j].transform.localRotation;
            }
        }
    }

    public void SetWallVisualActive(int index, bool isActive)
    {
        if (index < 0 || index >= wallObjects.Length) return;
        if (wallObjects[index] != null)
        {
            wallObjects[index].SetActive(isActive);
        }
    }

    public void ActivateDebrisWithForce(int index, Vector3 wallNormal, float explosionForce)
    {
        if (index < 0 || index >= preSpawnedDebris.Length) return;
        
        DebrisGroup group = preSpawnedDebris[index];
        if (group == null || group.pieces.Length == 0) return;

        group.rootObject.SetActive(true);

        Vector3 baseDirection = -wallNormal.normalized;
        float scatterRadius = 0.5f;

        for (int i = 0; i < group.pieces.Length; i++)
        {
            Rigidbody rb = group.pieces[i];
            
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 finalDirection = (baseDirection + Random.insideUnitSphere * scatterRadius).normalized;
            
            rb.AddForce(finalDirection * explosionForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * (explosionForce * 0.5f), ForceMode.Impulse);
        }
    }

    public void ResetAllDebris()
    {
        if (preSpawnedDebris == null) return;

        for (int i = 0; i < preSpawnedDebris.Length; i++)
        {
            DebrisGroup group = preSpawnedDebris[i];
            if (group == null || group.rootObject == null) continue;

            group.rootObject.SetActive(false);

            for (int j = 0; j < group.pieces.Length; j++)
            {
                Rigidbody rb = group.pieces[j];
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.transform.localPosition = group.initialPositions[j];
                rb.transform.localRotation = group.initialRotations[j];
            }
        }
    }
}