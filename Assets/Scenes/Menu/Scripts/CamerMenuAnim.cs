using UnityEngine;

public class CamerMenuAnim : MonoBehaviour
{
    [Header("Position Sway (Translation)")]
    [Tooltip("Maximum distance the camera can float on X, Y, and Z axes.")]
    public Vector3 positionSwayAmount = new Vector3(0.12f, 0.15f, 0.08f);
    [Tooltip("Speed multiplier for the position sway animation.")]
    public float positionSwaySpeed = 0.5f;

    [Header("Rotation Sway (Rotation)")]
    [Tooltip("Maximum rotation angle (in degrees) on Pitch, Yaw, and Roll axes.")]
    public Vector3 rotationSwayAmount = new Vector3(1.0f, 1.2f, 0.6f);
    [Tooltip("Speed multiplier for the rotation sway animation.")]
    public float rotationSwaySpeed = 0.4f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private void Start()
    {
        // Cache initial local transform to float relative to it
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        float time = Time.time;

        // Position Sway (using slightly offset frequencies for organic movement)
        float posX = Mathf.Sin(time * positionSwaySpeed) * positionSwayAmount.x;
        float posY = Mathf.Cos(time * positionSwaySpeed * 0.85f) * positionSwayAmount.y;
        float posZ = Mathf.Sin(time * positionSwaySpeed * 1.25f) * positionSwayAmount.z;
        Vector3 posOffset = new Vector3(posX, posY, posZ);

        transform.localPosition = initialLocalPosition + posOffset;

        // Rotation Sway (Pitch, Yaw, Roll - using offset frequencies)
        float rotX = Mathf.Sin(time * rotationSwaySpeed * 0.9f) * rotationSwayAmount.x;
        float rotY = Mathf.Cos(time * rotationSwaySpeed * 1.15f) * rotationSwayAmount.y;
        float rotZ = Mathf.Sin(time * rotationSwaySpeed * 0.75f) * rotationSwayAmount.z;
        Quaternion rotOffset = Quaternion.Euler(rotX, rotY, rotZ);

        transform.localRotation = initialLocalRotation * rotOffset;
    }
}
