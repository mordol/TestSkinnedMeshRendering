using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestTrajectoryUpdater : MonoBehaviour
{
    Material material;
    public Transform position;
    public Transform target;
    
    // Start is called before the first frame update
    void Start()
    {
        material = GetComponent<MeshRenderer>().sharedMaterial;
        
        // Make quad mesh with 1x1 size
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0, 0),
            new Vector3(0.5f, 0, 0),
            new Vector3(-0.5f, 0, 1f),
            new Vector3(0.5f, 0, 1f)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // Update is called once per frame
    void Update()
    {
        material.SetVector("_Position", position.position);
        material.SetVector("_TargetPosition", target.position);
    }
}
