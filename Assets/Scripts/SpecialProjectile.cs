using UnityEngine;

public class SpecialProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 60f;
    public float lifetime = 2.5f;

    [Header("Stun Effect")]
    public float stunDuration = 1.5f;

    private Vector3 moveDirection;
    private KartController owner;

    public void Initialize(KartController ownerKart, Vector3 direction)
    {
        owner = ownerKart;
        moveDirection = direction.normalized;
        
        // Orient the projectile visual to face its movement direction
        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
        
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += moveDirection * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject otherGo)
    {
        KartController targetKart = otherGo.GetComponentInParent<KartController>();
        if (targetKart != null)
        {
            // Do not hit the owner who launched it
            if (targetKart == owner)
                return;

            targetKart.HitBySpecial(stunDuration);
            Destroy(gameObject);
        }
    }
}
