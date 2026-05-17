using UnityEngine;
using System;

[ExecuteAlways]
public class CicloDiaNoite : MonoBehaviour
{
    public Light sol;

    [Header("Configuração de Tempo")]
    public bool usarTempoReal = true;

    [Range(0, 24)]
    public float horaCustomizada = 12f;

    [Range(0, 24)]
    [SerializeField]
    private float horaDoDia;

    [Header("Cor da Luz Solar")]
    public Gradient corDoSol;

    [Header("Intensidade da Luz Solar")]
    public AnimationCurve intensidadeDoSol;

    void Reset()
    {
        // Inicializa o Gradient (cor da luz ao longo do dia)
        corDoSol = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[5];
        colorKeys[0] = new GradientColorKey(new Color(0.05f, 0.05f, 0.2f), 0f);    // 00:00 - noite escura
        colorKeys[1] = new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.25f);     // 06:00 - amanhecer
        colorKeys[2] = new GradientColorKey(new Color(1f, 1f, 0.9f), 0.5f);        // 12:00 - dia
        colorKeys[3] = new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.75f);     // 18:00 - pôr do sol
        colorKeys[4] = new GradientColorKey(new Color(0.05f, 0.05f, 0.2f), 1f);    // 24:00 - noite escura

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);

        corDoSol.SetKeys(colorKeys, alphaKeys);

        // Inicializa a curva de intensidade da luz solar
        intensidadeDoSol = new AnimationCurve(
            new Keyframe(0f, 0f),     // 00:00
            new Keyframe(0.25f, 1f),  // 06:00
            new Keyframe(0.5f, 1.2f), // 12:00
            new Keyframe(0.75f, 1f),  // 18:00
            new Keyframe(1f, 0f)      // 24:00
        );
    }

    void Update()
    {
        // Atualiza a hora com base na opção
        if (usarTempoReal)
        {
            DateTime agora = DateTime.Now;
            horaDoDia = agora.Hour + agora.Minute / 60f;
        }
        else
        {
            horaDoDia = horaCustomizada;
        }

        AtualizarSol(horaDoDia);
    }

    void AtualizarSol(float hora)
    {
        float t = hora / 24f;
        float rotacao = t * 360f - 90f;

        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(new Vector3(rotacao, 170f, 0));
            sol.color = corDoSol.Evaluate(t);
            sol.intensity = intensidadeDoSol.Evaluate(t);
        }
    }
}