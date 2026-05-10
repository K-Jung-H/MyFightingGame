using UnityEngine;
using System.Collections.Generic;

public class DebrisGroup
{
    public GameObject rootObject; 
    public Rigidbody[] pieces;
}

public class StageWallAnimationController : MonoBehaviour
{
    public GameObject[] wallObjects;
    private DebrisGroup[] preSpawnedDebris;

    public void PreWarmDebris()
    {
        if (wallObjects == null) return;
        
        preSpawnedDebris = new DebrisGroup[wallObjects.Length];

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

                piece.layer = LayerMask.NameToLayer("VisualDebris"); 
                
                rbs.Add(rb);
            }

            preSpawnedDebris[i] = new DebrisGroup 
            {
                rootObject = cloneRoot,
                pieces = rbs.ToArray()
            };
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
}