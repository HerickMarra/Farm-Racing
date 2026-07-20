using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Necessário para os eventos de mouse (hover/click)

public class CardsSelectCar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Elements")]
    public Image carImage;          // O componente Image da UI (Canvas) que será alterado
    public Sprite carSprite;        // O Sprite do card do carro
    public GameObject lockObject;   // O GameObject do cadeado (ex: ícone de bloqueado)

    [Header("Settings")]
    public int carID;               // ID do carro para salvar na memória
    public bool isUnlocked = false; // Padrão: não desbloqueado (false)

    // Chave utilizada para salvar no PlayerPrefs
    private const string SELECTED_CAR_KEY = "SelectedCarID";

    // Variável estática compartilhada entre todos os cards para guardar o carro selecionado
    private static Sprite currentSelectedSprite;

    private Image selfImage;        // Componente Image do próprio GameObject onde o script está anexado

    void Awake()
    {
        selfImage = GetComponent<Image>();
    }

    void Start()
    {
        // Obtém o ID salvo no PlayerPrefs (se não existir, usa 1 como valor padrão)
        int savedID = PlayerPrefs.GetInt(SELECTED_CAR_KEY, 1);

        // Se este card é o carro salvo na memória e está desbloqueado, define ele como selecionado
        if (isUnlocked && savedID == carID)
        {
            if (carSprite != null)
            {
                currentSelectedSprite = carSprite;
                if (carImage != null)
                {
                    carImage.sprite = carSprite;
                }
            }
        }
        // Caso contrário, salva a imagem inicial do carImage como selecionada por padrão (se ainda não definida)
        else if (currentSelectedSprite == null && carImage != null && carImage.sprite != null)
        {
            currentSelectedSprite = carImage.sprite;
        }

        UpdateLockState();
    }

    // Controla o estado de ativação do cadeado e a cor da imagem
    public void UpdateLockState()
    {
        if (lockObject != null)
        {
            // Se estiver desbloqueado (isUnlocked == true) -> desativa o cadeado (false)
            // Se estiver bloqueado (isUnlocked == false)   -> ativa o cadeado (true)
            lockObject.SetActive(!isUnlocked);
        }

        // Define a cor: #FFFFFF se desbloqueado, #737373 (escuro) se bloqueado
        Color targetColor = isUnlocked ? Color.white : new Color32(0x73, 0x73, 0x73, 255);

        // Altera a cor na própria imagem do objeto onde o script está
        if (selfImage != null)
        {
            selfImage.color = targetColor;
        }

        // Altera também no carImage se estiver atribuído
        if (carImage != null)
        {
            carImage.color = targetColor;
        }
    }

    // Chamado quando o ponteiro do mouse entra na área do objeto UI
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Mostra a prévia do carro ao passar o mouse
        if (carImage != null && carSprite != null)
        {
            carImage.sprite = carSprite;
        }
    }

    // Chamado quando o ponteiro do mouse sai da área do objeto UI
    public void OnPointerExit(PointerEventData eventData)
    {
        // Ao tirar o mouse, volta SEMPRE para o carro que está atualmente SELECIONADO (clicado)
        if (carImage != null && currentSelectedSprite != null)
        {
            carImage.sprite = currentSelectedSprite;
        }
    }

    // Chamado ao clicar no card
    public void OnPointerClick(PointerEventData eventData)
    {
        // Só altera a seleção definitiva se o card estiver DESBLOQUEADO
        if (isUnlocked)
        {
            if (carSprite != null)
            {
                currentSelectedSprite = carSprite;
            }

            if (carImage != null && currentSelectedSprite != null)
            {
                carImage.sprite = currentSelectedSprite;
            }

            // Salva o ID do carro selecionado na memória
            PlayerPrefs.SetInt(SELECTED_CAR_KEY, carID);
            PlayerPrefs.Save();
            Debug.Log("Carro selecionado salvo no PlayerPrefs! ID: " + carID);
        }
    }
}


