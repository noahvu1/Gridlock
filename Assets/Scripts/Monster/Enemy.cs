using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class SimpleDumbMonster : MonoBehaviour
{
    [Header("Movement")]
    public float wanderSpeed = 1.5f;
    public float chaseSpeed = 3f;
    public float gravity = -9.81f;
    public float obstacleCheckDistance = 1.5f;
    public float wanderDirectionChangeTime = 3f;

    [Header("Detection / Combat")]
    public float detectionRadius = 12f;
    public float attackRange = 2f;
    public int damagePerHit = 10;
    public float attackCooldown = 1.2f;

    [Header("Optional Animation")]
    public string moveSpeedParam = "MoveSpeed";   // float param in Animator
    public string attackTriggerParam = "Attack";  // trigger param in Animator

    CharacterController _cc;
    Animator _anim;
    Vector3 _velocity;
    Vector3 _wanderDir;
    float _nextWanderDirTime;
    float _nextTargetSearchTime;
    float _nextAttackTime;

    Transform _target;

    enum State { Wander, Chase, Attack }
    State _state = State.Wander;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _anim = GetComponentInChildren<Animator>();

        // auto-fit character controller a bit so it stands on the ground
        if (_cc)
        {
            if (_cc.height < 0.5f) _cc.height = 2f;
            if (_cc.radius < 0.1f) _cc.radius = 0.5f;
            _cc.center = new Vector3(0f, _cc.height / 2f, 0f);
        }

        // if there is a CapsuleCollider on the monster, disable it so it
        // doesn't fight with the CharacterController
        CapsuleCollider cap = GetComponent<CapsuleCollider>();
        if (cap) cap.enabled = false;

        PickNewWanderDirection();
    }

    void Update()
    {
        UpdateTarget();
        UpdateState();
        RunState();
        ApplyGravity();

        if (_cc)
        {
            _cc.Move(_velocity * Time.deltaTime);
        }

        StickToGround();
        UpdateAnimator();
    }

    void UpdateTarget()
    {
        if (Time.time < _nextTargetSearchTime) return;
        _nextTargetSearchTime = Time.time + 0.5f;

        PlayerHealthUI[] players = FindObjectsOfType<PlayerHealthUI>();
        Transform best = null;
        float bestDist = Mathf.Infinity;
        Vector3 pos = transform.position;

        foreach (var p in players)
        {
            if (!p || p.IsDead()) continue;

            float d = Vector3.Distance(pos, p.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = p.transform.root;
            }
        }

        _target = best;
    }

    void UpdateState()
    {
        if (_target == null)
        {
            _state = State.Wander;
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist <= attackRange)
            _state = State.Attack;
        else if (dist <= detectionRadius)
            _state = State.Chase;
        else
            _state = State.Wander;
    }

    void RunState()
    {
        switch (_state)
        {
            case State.Wander:
                DoWander();
                break;
            case State.Chase:
                DoChase();
                break;
            case State.Attack:
                DoAttack();
                break;
        }
    }

    void DoWander()
    {
        if (Time.time >= _nextWanderDirTime)
            PickNewWanderDirection();

        // avoid wall straight ahead
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward,
                            obstacleCheckDistance))
        {
            PickNewWanderDirection();
        }

        Vector3 move = _wanderDir * wanderSpeed;
        _velocity.x = move.x;
        _velocity.z = move.z;

        if (_wanderDir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(_wanderDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
        }
    }

    void DoChase()
    {
        if (_target == null)
        {
            DoWander();
            return;
        }

        Vector3 dir = _target.position - transform.position;
        dir.y = 0f;
        dir.Normalize();

        // simple obstacle avoidance while chasing
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward,
                            obstacleCheckDistance))
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            dir = (dir + side * 0.7f).normalized;
        }

        Vector3 move = dir * chaseSpeed;
        _velocity.x = move.x;
        _velocity.z = move.z;

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 10f);
        }
    }

    void DoAttack()
    {
        if (_target == null)
        {
            _state = State.Wander;
            return;
        }

        Vector3 dir = _target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 10f);
        }

        _velocity.x = 0f;
        _velocity.z = 0f;

        if (Time.time >= _nextAttackTime)
        {
            _nextAttackTime = Time.time + attackCooldown;

            if (_anim && !string.IsNullOrEmpty(attackTriggerParam))
                _anim.SetTrigger(attackTriggerParam);

            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist <= attackRange + 0.5f)
            {
                PlayerHealthUI health = _target.GetComponentInChildren<PlayerHealthUI>();
                if (health != null)
                    health.TakeDamage(damagePerHit);
            }
        }
    }

    void PickNewWanderDirection()
    {
        Vector2 circle = Random.insideUnitCircle.normalized;
        _wanderDir = new Vector3(circle.x, 0f, circle.y);
        _nextWanderDirTime = Time.time + wanderDirectionChangeTime;
    }

    void ApplyGravity()
    {
        if (_cc == null) return;

        if (_cc.isGrounded)
        {
            _velocity.y = -8f; // keep it pressed to the floor
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }
    }

    void StickToGround()
    {
        // small raycast to glue to ground / stairs
        Ray ray = new Ray(transform.position + Vector3.up * 1f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f))
        {
            Vector3 pos = transform.position;
            float targetY = hit.point.y;
            pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * 10f);
            transform.position = pos;
        }
    }

    void UpdateAnimator()
    {
        if (_anim == null) return;

        if (!string.IsNullOrEmpty(moveSpeedParam))
        {
            Vector3 horizontalVel = _velocity;
            horizontalVel.y = 0f;
            _anim.SetFloat(moveSpeedParam, horizontalVel.magnitude);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
