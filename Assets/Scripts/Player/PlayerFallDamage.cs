using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerFallDamage : MonoBehaviour
{
    // refs
    public PlayerHealthUI health;     // drag your PlayerHealthUI here
    Rigidbody _rb;

    [Header("Ground Check")]
    public string groundTag = "Ground";
    public float groundNormalMinY = 0.5f;

    [Header("Damage Tuning")]
    public float safeSpeed = 6f;         // lowered a bit so falls hurt more
    public float lethalSpeed = 20f;      // adjust to taste
    public int maxDamageAtLethal = 100;
    public float cooldown = 0.25f;

    float _lastHitTime;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!health) health = GetComponent<PlayerHealthUI>();
    }

    void OnCollisionEnter(Collision c)
    {
        if (!health || health.IsDead())
        {
            Debug.Log("[FallDamage] No health component or already dead.");
            return;
        }

        if (Time.time < _lastHitTime + cooldown)
        {
            Debug.Log("[FallDamage] On cooldown.");
            return;
        }

        // check ground contact
        bool groundedContact = false;
        foreach (var contact in c.contacts)
        {
            if (contact.otherCollider.CompareTag(groundTag) &&
                contact.normal.y >= groundNormalMinY)
            {
                groundedContact = true;
                break;
            }
        }

        if (!groundedContact)
        {
            Debug.Log("[FallDamage] Collision, but NOT ground.");
            return;
        }

        // use collision.relativeVelocity to measure actual impact
        Vector3 relVel = c.relativeVelocity;
        float verticalSpeed = Vector3.Project(relVel, Vector3.down).magnitude;
        float totalSpeed = relVel.magnitude;

        Debug.Log($"[FallDamage] Impact totalSpeed={totalSpeed:F2}, verticalSpeed={verticalSpeed:F2}");

        float impactSpeed = verticalSpeed; // or totalSpeed if you prefer

        if (impactSpeed <= safeSpeed)
        {
            Debug.Log($"[FallDamage] Safe landing. impactSpeed={impactSpeed:F2} <= safeSpeed={safeSpeed:F2}");
            return;
        }

        // scale 0..1 across [safeSpeed, lethalSpeed]
        float t = Mathf.InverseLerp(safeSpeed, lethalSpeed, impactSpeed);
        int dmg = Mathf.CeilToInt(t * maxDamageAtLethal);

        Debug.Log($"[FallDamage] Applying damage: {dmg} (impactSpeed={impactSpeed:F2})");

        if (dmg > 0)
        {
            health.TakeDamage(dmg);
            _lastHitTime = Time.time;
        }
    }
}
