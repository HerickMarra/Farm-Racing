using UnityEngine;

public class AviaoController : MonoBehaviour
{
    [Header("Flight Settings")]
    [Tooltip("Lista de pontos por onde o avião vai passar.")]
    public Transform[] waypoints;
    
    [Tooltip("Velocidade de movimento do avião.")]
    public float speed = 25f;
    
    [Tooltip("Velocidade com que o avião se vira na direção do ponto.")]
    public float rotationSpeed = 3f;
    
    [Tooltip("Fator de inclinação (roll) lateral ao fazer curvas. Maior = mais inclinação.")]
    public float bankingFactor = 45f;
    
    [Tooltip("Distância mínima para considerar que o avião chegou ao ponto.")]
    public float reachDistance = 5f;

    private int currentWaypointIndex = 0;

    void Start()
    {
        // Se houver pontos e o avião não estiver posicionado no primeiro, inicia lá
        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
        {
            transform.position = waypoints[0].position;
            // Olha em direção ao segundo ponto
            if (waypoints.Length > 1 && waypoints[1] != null)
            {
                transform.rotation = Quaternion.LookRotation(waypoints[1].position - transform.position);
            }
            currentWaypointIndex = 1 % waypoints.Length;
        }
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform targetWaypoint = waypoints[currentWaypointIndex];
        if (targetWaypoint == null)
        {
            // Pula pontos vazios na lista
            GoToNextWaypoint();
            return;
        }

        // 1. Movimento em direção ao ponto
        Vector3 targetPos = targetWaypoint.position;
        Vector3 direction = (targetPos - transform.position).normalized;
        
        // Movimenta para a frente
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);

        // 2. Rotação em direção ao ponto com efeito de inclinação (banking)
        if (direction != Vector3.zero)
        {
            // Rotação de olhar básica (yaw/pitch) em direção ao alvo
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // Calcula o ângulo horizontal (Yaw) de curva para determinar a inclinação lateral (Roll)
            // Projetamos a direção para entender a curva apenas no plano horizontal
            Vector3 localTargetDir = transform.InverseTransformDirection(direction);
            float turnAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            
            // Quanto mais fechada a curva, maior o roll (inclinação lateral)
            // Clampamos para o avião não dar piruetas de 180 graus de lado de repente
            float roll = -turnAngle * bankingFactor * 0.1f;
            roll = Mathf.Clamp(roll, -bankingFactor, bankingFactor);
            
            // Aplica a inclinação no eixo Z relativo à rotação alvo
            Vector3 eulerAngles = targetRotation.eulerAngles;
            targetRotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, roll);

            // Suaviza a rotação geral do avião
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 3. Verificação de chegada ao ponto
        float distanceToWaypoint = Vector3.Distance(transform.position, targetPos);
        if (distanceToWaypoint <= reachDistance)
        {
            GoToNextWaypoint();
        }
    }

    private void GoToNextWaypoint()
    {
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    private void OnDrawGizmos()
    {
        // Desenha a rota de voo no editor da Unity para ajudar a visualizar o caminho
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            
            Gizmos.DrawSphere(waypoints[i].position, 1.5f);
            
            int nextIdx = (i + 1) % waypoints.Length;
            if (waypoints[nextIdx] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[nextIdx].position);
            }
        }
    }
}
