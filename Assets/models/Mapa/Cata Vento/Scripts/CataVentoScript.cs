using UnityEngine;

public class CataVentoScript : MonoBehaviour
{
    public enum EixoRotacao
    {
        X,
        Y,
        Z
    }

    [Header("Configurações")]
    public EixoRotacao eixo = EixoRotacao.Y;
    public float velocidade = 20f;

    void Update()
    {
        Vector3 direcao = Vector3.zero;

        switch (eixo)
        {
            case EixoRotacao.X:
                direcao = Vector3.right;
                break;

            case EixoRotacao.Y:
                direcao = Vector3.up;
                break;

            case EixoRotacao.Z:
                direcao = Vector3.forward;
                break;
        }

        transform.Rotate(direcao * velocidade * Time.deltaTime);
    }
}