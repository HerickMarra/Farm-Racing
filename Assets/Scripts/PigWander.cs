using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class PigWander : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("How far the pig can wander from its initial starting position.")]
    [SerializeField] private float wanderRadius = 15f;
    
    [Tooltip("Minimum time to wait (idle) before moving to the next spot.")]
    [SerializeField] private float minWaitTime = 2f;
    
    [Tooltip("Maximum time to wait (idle) before moving to the next spot.")]
    [SerializeField] private float maxWaitTime = 6f;

    [Tooltip("Movement speed of the pig.")]
    [SerializeField] private float movementSpeed = 1.5f;

    private NavMeshAgent agent;
    private Animator animator;
    private Vector3 homePosition;
    private bool isWaiting = false;

    private static readonly int WalkHash = Animator.StringToHash("walk");

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        // Cache initial position as the reference point for wandering
        homePosition = transform.position;

        // Apply movement speed to the NavMeshAgent
        if (agent != null)
        {
            agent.speed = movementSpeed;
            // Enable auto-braking so the pig slows down naturally as it arrives
            agent.autoBraking = true;
        }

        // Start wandering
        StartCoroutine(WanderRoutine());
    }

    private void Update()
    {
        if (agent == null || animator == null) return;

        // Determine if the pig is walking based on its actual velocity and remaining distance
        bool isWalking = agent.velocity.sqrMagnitude > 0.01f && agent.remainingDistance > agent.stoppingDistance;
        
        // Update animator parameter
        animator.SetBool(WalkHash, isWalking);
    }

    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                if (!isWaiting)
                {
                    StartCoroutine(WaitAndMove());
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator WaitAndMove()
    {
        isWaiting = true;

        // Wait for a random duration
        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitTime);

        // Pick a new random destination near home position
        Vector3 newTarget = GetRandomPositionNearHome();
        if (newTarget != Vector3.zero)
        {
            agent.SetDestination(newTarget);
        }

        isWaiting = false;
    }

    private Vector3 GetRandomPositionNearHome()
    {
        for (int i = 0; i < 30; i++) // Try up to 30 times to find a valid spot
        {
            Vector3 randomPoint = homePosition + Random.insideUnitSphere * wanderRadius;
            
            // Sample the position on the NavMesh
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        return Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw the wander boundary in the editor
        Gizmos.color = Color.green;
        Vector3 center = Application.isPlaying ? homePosition : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}
