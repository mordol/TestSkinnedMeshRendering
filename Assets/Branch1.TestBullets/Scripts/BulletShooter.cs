using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float fireRate = 0.2f; // 총알 발사 속도 (초당 발사 횟수)
    public float angleRange = 60f; // 총알이 발사되는 각도 범위
    public float pingPongSpeed = 2f; // Ping Pong 속도
    public int spawnCount = 1000;
    public float bulletDistance = 20f;
    
    [SerializeField, ReadOnly]
    int bulletDataCount = 0;
    

    float fireTimer = 0f;
    bool isFiring = false;
    List<BulletData> bulletDataList = new List<BulletData>();

    class BulletData
    {
        public bool active;
        public GameObject bullet;
        public Material material;
        
        public Vector3 position;
        public Vector3 direction;
    }

    Mesh quadMesh;
    int propertyIdPosition;
    int propertyIdTargetPosition;
    
    void Start()
    {
        propertyIdPosition = Shader.PropertyToID("_Position");
        propertyIdTargetPosition = Shader.PropertyToID("_TargetPosition");
        
        // Make quad mesh with 1x1 size
        quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0, 0),
            new Vector3(0.5f, 0, 0),
            new Vector3(-0.5f, 0, 1f),
            new Vector3(0.5f, 0, 1f)
        };
        quadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            isFiring = true;
            fireTimer = 0f;
        }

        if (Input.GetButtonUp("Fire1"))
        {
            isFiring = false;
        }

        if (isFiring)
        {
            fireTimer += Time.deltaTime;

            if (fireTimer >= fireRate)
            {
                fireTimer = FireBullet(fireTimer);
            }
        }
        
        UpdateBulletPositions();
        
        bulletDataCount = bulletDataList.Count;

        if (Input.GetKeyUp(KeyCode.Alpha1))
        {
            print($"active: {bulletDataList.Count(t => t.active)}");
        }
    }

    float FireBullet(float fireTimer)
    {
        var fireCount = Mathf.FloorToInt(fireTimer / fireRate);
        float remainTime = fireTimer % fireRate;
        float currentTime = Time.time;

        for (int i = 0; i < fireCount; i++)
        {
            float bulletTime = currentTime - (fireCount - 1 - i) * fireRate;
            float oscillationTime = bulletTime * pingPongSpeed;
            float angle = Mathf.PingPong(oscillationTime, angleRange) - (angleRange / 2f);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            var bulletData = GetNewBulletData();
            bulletData.direction = direction;
            bulletData.position = transform.position;
            //bulletData.bullet.transform.SetPositionAndRotation(bulletData.position, Quaternion.LookRotation(direction));
        }

        return remainTime;
    }
    
    BulletData GetNewBulletData()
    {
        foreach (var t in bulletDataList)
        {
            if (!t.active)
            {
                t.active = true;
                t.bullet.SetActive(true);
                return t;
            }
        }
        
        var bulletData = new BulletData
        {
            active = true,
            bullet = Instantiate(bulletPrefab)
        };
        
        var bullet = bulletData.bullet;
        bullet.SetActive(true);
        bullet.GetComponent<MeshFilter>().sharedMesh = quadMesh;
        bulletData.material = bullet.GetComponent<MeshRenderer>().material;
        
        bulletDataList.Add(bulletData);
        return bulletData;
    }
    
    void UpdateBulletPositions()
    {
        foreach (var bulletData in bulletDataList)
        {
            if (!bulletData.active)
                continue;
            
            bulletData.position += bulletData.direction * (Time.deltaTime * 5f); // 총알 속도 설정 (10은 임의의 값입니다)
            //bulletData.bullet.transform.position = bulletData.position;
            bulletData.material.SetVector(propertyIdPosition, bulletData.position);
            bulletData.material.SetVector(propertyIdTargetPosition, bulletData.position + bulletData.direction);
            
            // 총알이 화면 밖으로 나가면 비활성화
            if (bulletData.position.magnitude > bulletDistance)
            {
                bulletData.active = false;
                bulletData.bullet.SetActive(false);
            }
        }
    }
}