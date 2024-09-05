using System;
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
    
    public bool useMatrix = false;

    private Mesh quad;
    private Mesh quadModifed;

    private void Start()
    {
        quad = GetComponent<MeshFilter>().sharedMesh;
    }

    private void Update()
    {
        if (useMatrix)
        {
            CalculateWith4x4Matrix();
        }
        else
        {
            CalculateWithTransform();
        }
    }

    void CalculateWithTransform()
    {
        GetComponent<MeshFilter>().sharedMesh = quad;
        
        Vector3 targetPos = target.position;
        Vector3 directionPos = direction.position;
        Vector3 directionVector = Vector3.Normalize(directionPos - targetPos);
        Vector3 normal = Vector3.Cross(directionVector, Vector3.up);
        Vector3 up = Vector3.Cross(normal, directionVector);
        Vector3 right = Vector3.Cross(directionVector, up);
        
        transform.position = targetPos + directionVector * (distance * -0.5f);
        transform.localScale = new Vector3(0.1f, distance, 1f);
        transform.rotation = Quaternion.LookRotation(directionVector, up) * Quaternion.Euler(-90f, 0, 0);
    }

    void CalculateWith4x4Matrix()
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
        
        // // Scale matrix
        // Matrix4x4 scaleMatrix = new Matrix4x4();
        // scaleMatrix.SetRow(0, new Vector4(0.1f, 0.0f, 0.0f, 0.0f));
        // scaleMatrix.SetRow(1, new Vector4(0.0f, distance, 0.0f, 0.0f));
        // scaleMatrix.SetRow(2, new Vector4(0.0f, 0.0f, 1.0f, 0.0f));
        // scaleMatrix.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        
        // Rotation matrix (quaternion to matrix)
        var rotation = Quaternion.LookRotation(directionVector, up) * Quaternion.Euler(-90f, 0, 0);
        // float xx = rotation.x * rotation.x;
        // float yy = rotation.y * rotation.y;
        // float zz = rotation.z * rotation.z;
        // float xy = rotation.x * rotation.y;
        // float xz = rotation.x * rotation.z;
        // float yz = rotation.y * rotation.z;
        // float wx = rotation.w * rotation.x;
        // float wy = rotation.w * rotation.y;
        // float wz = rotation.w * rotation.z;
        //
        // Matrix4x4 rotationMatrix = new Matrix4x4();
        // rotationMatrix.SetRow(0, new Vector4(1.0f - 2.0f * (yy + zz), 2.0f * (xy + wz),     2.0f * (xz - wy),     0.0f));
        // rotationMatrix.SetRow(1, new Vector4(2.0f * (xy - wz),        1.0f - 2.0f * (xx + zz), 2.0f * (yz + wx),     0.0f));
        // rotationMatrix.SetRow(2, new Vector4(2.0f * (xz + wy),        2.0f * (yz - wx),     1.0f - 2.0f * (xx + yy), 0.0f));
        // rotationMatrix.SetRow(3, new Vector4(0.0f,                   0.0f,                 0.0f,                 1.0f));
        
        // Translation matrix
        var position = targetPos + directionVector * (distance * -0.5f);
        // Matrix4x4 translationMatrix = new Matrix4x4();
        // translationMatrix.SetRow(0, new Vector4(1.0f, 0.0f, 0.0f, position.x));
        // translationMatrix.SetRow(1, new Vector4(0.0f, 1.0f, 0.0f, position.y));
        // translationMatrix.SetRow(2, new Vector4(0.0f, 0.0f, 1.0f, position.z));
        // translationMatrix.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        
        // Combine all transformations: T * R * S
        //var TRS = translationMatrix * (rotationMatrix * scaleMatrix);
        //var TRS = Matrix4x4.TRS(position, rotation, new Vector3(0.1f, distance, 1f));
        var TRS = SetTRS(position, rotation, new Vector3(0.1f, distance, 1f));
        
        // Test
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        
        var meshFilter = GetComponent<MeshFilter>();
        var mesh = new Mesh();
        mesh.vertices = Array.ConvertAll(quad.vertices, v => TRS.MultiplyPoint3x4(v));
        mesh.normals = Array.ConvertAll(quad.normals, n => TRS.MultiplyVector(n));
        mesh.triangles = quad.triangles;
        mesh.uv = quad.uv;
        meshFilter.mesh = mesh;
    }
    
    Matrix4x4 QuaternionToMatrix(Quaternion q)
    {
        // If q is guaranteed to be a unit quaternion, s will always
        // be 1.  In that case, this calculation can be optimized out.

        // Precalculate coordinate products
        float x = q.x * 2.0f;
        float y = q.y * 2.0f;
        float z = q.z * 2.0f;
        float xx = q.x * x;
        float yy = q.y * y;
        float zz = q.z * z;
        float xy = q.x * y;
        float xz = q.x * z;
        float yz = q.y * z;
        float wx = q.w * x;
        float wy = q.w * y;
        float wz = q.w * z;

        // Calculate 3x3 matrix from orthonormal basis
        var m = new Matrix4x4();
        
        m.m00 = 1.0f - (yy + zz);
        m.m10 = xy + wz;
        m.m20 = xz - wy;
        m.m30 = 0.0f;

        m.m01 = xy - wz;
        m.m11 = 1.0f - (xx + zz);
        m.m21 = yz + wx;
        m.m31 = 0.0F;

        m.m02  = xz + wy;
        m.m12  = yz - wx;
        m.m22 = 1.0f - (xx + yy);
        m.m32 = 0.0f;

        m.m03 = 0.0f;
        m.m13 = 0.0f;
        m.m23 = 0.0f;
        m.m33 = 1.0f;
        
        return m;
    }
    
    Matrix4x4 SetTRS(Vector3 pos, Quaternion q, Vector3 s)
    {
        var m = QuaternionToMatrix(q);

        m.m00 *= s[0];
        m.m10 *= s[0];
        m.m20 *= s[0];

        m.m01 *= s[1];
        m.m11 *= s[1];
        m.m21 *= s[1];

        m.m02 *= s[2];
        m.m12 *= s[2];
        m.m22 *= s[2];

        m.m03 = pos[0];
        m.m13 = pos[1];
        m.m23 = pos[2];
        
        return m;
    }
}
