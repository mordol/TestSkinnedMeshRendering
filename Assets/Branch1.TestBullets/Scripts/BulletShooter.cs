using System.Collections.Generic;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float fireRate = 0.2f; // 총알 발사 속도 (초당 발사 횟수)
    public float angleRange = 60f; // 총알이 발사되는 각도 범위
    public float pingPongSpeed = 2f; // Ping Pong 속도
    public int spawnCount = 1000;

    float fireTimer = 0f;
    bool isFiring = false;
    List<Vector3> fireVectors = new List<Vector3>();
    
    void Start()
    {
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
                fireTimer = 0f;
                FireBullet();
            }
        }
    }

    void FireBullet()
    {
        // 각도 계산
        float angle = Mathf.PingPong(Time.time * pingPongSpeed, angleRange) - (angleRange / 2f);
        Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

        // // 발사 벡터를 리스트에 추가
        // fireVectors.Add(direction);
        
        // 총알 발사
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.LookRotation(direction));
        bullet.SetActive(true);
        
        // 총알에 힘을 가하는 등의 추가 작업이 필요할 수 있습니다.
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.velocity = direction * 10f; // 총알 속도 설정 (10은 임의의 값입니다)
        }
    }
}
