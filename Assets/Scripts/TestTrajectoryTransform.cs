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
        
        // transform.position = targetPos + directionVector * (distance * -0.5f);
        // transform.localScale = new Vector3(0.1f, distance, 1f);
        // transform.rotation = Quaternion.LookRotation(directionVector, up) * Quaternion.Euler(-90f, 0, 0);
        
        // Scale matrix
        Matrix4x4 scaleMatrix = new Matrix4x4();
        scaleMatrix.SetRow(0, new Vector4(0.1f, 0.0f, 0.0f, 0.0f));
        scaleMatrix.SetRow(1, new Vector4(0.0f, distance, 0.0f, 0.0f));
        scaleMatrix.SetRow(2, new Vector4(0.0f, 0.0f, 1.0f, 0.0f));
        scaleMatrix.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        
        // Rotation matrix (quaternion to matrix)
        var rotation = Quaternion.LookRotation(directionVector, up) * Quaternion.Euler(-90f, 0, 0);
        float xx = rotation.x * rotation.x;
        float yy = rotation.y * rotation.y;
        float zz = rotation.z * rotation.z;
        float xy = rotation.x * rotation.y;
        float xz = rotation.x * rotation.z;
        float yz = rotation.y * rotation.z;
        float wx = rotation.w * rotation.x;
        float wy = rotation.w * rotation.y;
        float wz = rotation.w * rotation.z;

        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetRow(0, new Vector4(1.0f - 2.0f * (yy + zz), 2.0f * (xy + wz),     2.0f * (xz - wy),     0.0f));
        rotationMatrix.SetRow(1, new Vector4(2.0f * (xy - wz),        1.0f - 2.0f * (xx + zz), 2.0f * (yz + wx),     0.0f));
        rotationMatrix.SetRow(2, new Vector4(2.0f * (xz + wy),        2.0f * (yz - wx),     1.0f - 2.0f * (xx + yy), 0.0f));
        rotationMatrix.SetRow(3, new Vector4(0.0f,                   0.0f,                 0.0f,                 1.0f));
        
        // Translation matrix
        var position = targetPos + directionVector * (distance * -0.5f);
        Matrix4x4 translationMatrix = new Matrix4x4();
        translationMatrix.SetRow(0, new Vector4(1.0f, 0.0f, 0.0f, position.x));
        translationMatrix.SetRow(1, new Vector4(0.0f, 1.0f, 0.0f, position.y));
        translationMatrix.SetRow(2, new Vector4(0.0f, 0.0f, 1.0f, position.z));
        translationMatrix.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        
        // Combine all transformations: T * R * S
        var TRS = translationMatrix * (rotationMatrix * scaleMatrix);
        
        // Test
        Vector3 scale = new Vector3(
            TRS.GetColumn(0).magnitude,
            TRS.GetColumn(1).magnitude,
            TRS.GetColumn(2).magnitude
        );
        
        if (Vector3.Cross (TRS.GetColumn (0), TRS.GetColumn (1)).normalized != (Vector3)TRS.GetColumn (2).normalized)
        {
            scale.x *= -1;
        }
        
        Vector3 forward;
        forward.x = TRS.m02;
        forward.y = TRS.m12;
        forward.z = TRS.m22;
 
        Vector3 upwards;
        upwards.x = TRS.m01;
        upwards.y = TRS.m11;
        upwards.z = TRS.m21;
        
        //transform.rotation = Quaternion.LookRotation(TRS.GetColumn(2), TRS.GetColumn(1));
        transform.rotation = Quaternion.LookRotation(forward, upwards);
        transform.position = TRS.GetColumn(3);
        transform.localScale = scale;
    }
}
