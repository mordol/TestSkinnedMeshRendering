using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Computing the position and orientation of a quad for trajectory representation.
// Calculate with the Billboard method
public class TestTrajectoryTransform : MonoBehaviour
{
    public Transform cameraTransform;
    public Transform target;
    public Transform direction;
    public float distance = 1.0f;
    
    void Update()
    {
        Vector3 targetPos = target.position;
        Vector3 directionPos = direction.position;
        Vector3 directionVector = Vector3.Normalize(directionPos - targetPos);
        Vector3 normal = Vector3.Cross(directionVector, Vector3.up);
        Vector3 up = Vector3.Cross(normal, directionVector);
        Vector3 right = Vector3.Cross(directionVector, up);
        
        // Matrix4x4 matrix = new Matrix4x4();
        // matrix.SetColumn(0, right.normalized);
        // matrix.SetColumn(1, up.normalized);
        // matrix.SetColumn(2, directionVector.normalized);
        // matrix.SetColumn(3, targetPos + directionVector.normalized * distance);
        
        transform.position = targetPos + directionVector * (distance * -0.5f);
        transform.localScale = new Vector3(0.1f, distance, 1f);
        transform.rotation = Quaternion.LookRotation(directionVector, up) * Quaternion.Euler(-90f, 0, 0);
    }
}
