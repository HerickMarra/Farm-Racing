using System.Collections;
using UnityEngine;

public class CaixaSurpresa : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Velocidade de rotação ao redor do eixo vertical.")]
    [SerializeField] private float rotationSpeed = 50f;
    [Tooltip("Eixo de rotação (padrão é o eixo Y/Up).")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Header("Floating Settings")]
    [Tooltip("A amplitude do movimento de subida e descida (distância máxima a partir do ponto inicial).")]
    [SerializeField] private float floatAmplitude = 0.25f;
    [Tooltip("A velocidade com que a caixa sobe e desce (frequência do movimento).")]
    [SerializeField] private float floatFrequency = 1.5f;

    [Header("Item Box Cooldown")]
    [Tooltip("Tempo mínimo de recarga para a caixa reaparecer.")]
    [SerializeField] private float minCooldown = 3.0f;
    [Tooltip("Tempo máximo de recarga para a caixa reaparecer.")]
    [SerializeField] private float maxCooldown = 6.0f;

    [Header("Visual & Light Elements")]
    [Tooltip("Lista de GameObjects filhos (modelos, luzes, partículas) que serão desativados durante o tempo de recarga. Se deixado vazio, o script desativará todos os objetos filhos automaticamente.")]
    [SerializeField] private GameObject[] visualObjects;

    [Header("Special Abilities Pool")]
    [Tooltip("Pool of special abilities that can be dropped by this box. If empty, the kart's pre-configured item is used.")]
    [SerializeField] private SpecialAbility[] specialAbilitiesPool;

    private Vector3 startPosition;
    private float timeOffset;
    private bool isReady = true;

    private Collider col;

    private void Start()
    {
        col = GetComponent<Collider>();

        // Se a lista de objetos visuais estiver vazia, captura automaticamente todos os filhos diretos
        if (visualObjects == null || visualObjects.Length == 0)
        {
            visualObjects = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                visualObjects[i] = transform.GetChild(i).gameObject;
            }
        }

        // Salva a posição inicial local para servir de âncora para o movimento flutuante
        startPosition = transform.localPosition;
        
        // Gera um offset de tempo aleatório para que diferentes caixas na pista
        // não flutuem em sincronia perfeita (dando um aspecto mais orgânico e polido)
        timeOffset = Random.Range(0f, 100f);
    }

    private void Update()
    {
        // Só anima se a caixa estiver ativa
        if (!isReady) return;

        // 1. Rotação contínua e suave
        transform.Rotate(rotationAxis * (rotationSpeed * Time.deltaTime), Space.Self);

        // 2. Flutuação suave (onda senoidal) a partir da posição inicial
        float newY = startPosition.y + Mathf.Sin((Time.time + timeOffset) * floatFrequency) * floatAmplitude;
        transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Se a caixa não estiver pronta, ignora colisões
        if (!isReady) return;

        // Procura por um componente KartController no objeto que colidiu (ou nos pais dele)
        KartController kart = other.GetComponentInParent<KartController>();
        if (kart != null)
        {
            // Ativa o especial no kart
            kart.hasSpecial = true;

            // Se houver habilidades no pool, escolhe uma aleatoriamente e atribui
            if (specialAbilitiesPool != null && specialAbilitiesPool.Length > 0)
            {
                kart.currentSpecial = specialAbilitiesPool[Random.Range(0, specialAbilitiesPool.Length)];
            }

            // Escolhe aleatoriamente adicionar 1, 2 ou 3 (total) cargas ao medidor de boost
            int randomCharges = Random.Range(1, 4); // Retorna 1, 2 ou 3
            kart.AddBoostCharges(randomCharges);

            // Inicia o processo de desativação temporária da caixa
            StartCoroutine(DeactivateRoutine());
        }
    }

    private IEnumerator DeactivateRoutine()
    {
        isReady = false;

        // Desativa o colisor físico para impedir novas colisões
        if (col != null) col.enabled = false;

        // Desativa todos os objetos visuais e de luzes (filhos)
        foreach (var obj in visualObjects)
        {
            if (obj != null) obj.SetActive(false);
        }

        // Aguarda um tempo de recarga aleatório
        float cooldown = Random.Range(minCooldown, maxCooldown);
        yield return new WaitForSeconds(cooldown);

        // Reativa o colisor
        if (col != null) col.enabled = true;

        // Reativa todos os objetos visuais e de luzes (filhos)
        foreach (var obj in visualObjects)
        {
            if (obj != null) obj.SetActive(true);
        }

        isReady = true;
    }
}
