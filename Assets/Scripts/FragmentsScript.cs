using UnityEngine;

/// <summary>
/// Simple and highly optimized destruction system.
/// When this object collides with the player (Tag "Player"), it swaps the
/// intact mesh for a pre‑prepared set of fragments. The swap occurs only once.
/// </summary>
public class FragmentsScript : MonoBehaviour
{
    // Reference to the intact version of the object (the mesh/model that is visible initially).
    [SerializeField]
    private GameObject intactObject;

    // References to the fragments containers that should be activated on break.
    [SerializeField]
    private GameObject[] fragmentsObjects;

    // Flag to ensure we break only once.
    private bool isBroken = false;

    // Cache the transform to avoid repeated GetComponent calls (not strictly required but kept for potential future use).
    private Transform cachedTransform;

    // Time (in seconds) after which the broken object will be destroyed.
    // Adjustable in the Inspector; default is 5 seconds.
    [SerializeField]
    private float destroyDelay = 5f;

    // Optional AudioSource component to play a sound on impact.
    [SerializeField]
    private AudioSource breakAudioSource;



    // Pitch variation range for the destruction sound on start.
    [SerializeField]
    private float minPitch = 0.8f;
    [SerializeField]
    private float maxPitch = 1.2f;

    private void Awake()
    {
        // Cache commonly used components/transform.
        cachedTransform = transform;

        // If not assigned in Inspector, try to grab one on the GameObject.
        if (breakAudioSource == null)
        {
            breakAudioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        // Play the destruction sound with a random pitch each time the object starts.
        if (breakAudioSource != null)
        {
            breakAudioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to the Player or NPC, and ensure we haven't already broken.
        if (!isBroken && (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("NPC")))
        {
            Break();
        }
    }

    /// <summary>
    /// Handles the swap between the intact object and the fragments.
    /// </summary>
    private void Break()
    {
        isBroken = true;

        // Play the breaking sound if an AudioSource is available
        if (breakAudioSource != null)
        {
            breakAudioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            breakAudioSource.Play();
        }

        // Deactivate the intact representation if it exists.
        if (intactObject != null)
        {
            intactObject.SetActive(false);
        }

        // Activate all pre‑baked fragment objects if they exist.
        if (fragmentsObjects != null)
        {
            foreach (var fragment in fragmentsObjects)
            {
                if (fragment != null)
                {
                    fragment.SetActive(true);
                }
            }
        }

        // Schedule self‑destruction after the configured delay.
        // This ensures the broken object is removed from the scene without further processing.
        Destroy(gameObject, destroyDelay);
    }
}
