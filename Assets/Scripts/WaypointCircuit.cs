using UnityEngine;

public class WaypointCircuit : MonoBehaviour
{
    [Header("Circuit Settings")]
    public bool loop = true;
    public Color gizmoColor = Color.green;
    public float gizmoRadius = 1.0f;

    public Transform[] waypoints;

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            // Try to auto-populate from children if none assigned
            PopulateFromChildren();
        }

        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = gizmoColor;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            // Draw waypoint sphere
            Gizmos.DrawSphere(waypoints[i].position, gizmoRadius);

            // Draw line to next waypoint
            if (i < waypoints.Length - 1)
            {
                if (waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }
            else if (loop && waypoints[0] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
            }
        }
    }

    [ContextMenu("Populate From Children")]
    public void PopulateFromChildren()
    {
        int childCount = transform.childCount;
        waypoints = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            waypoints[i] = transform.GetChild(i);
        }
    }
}
