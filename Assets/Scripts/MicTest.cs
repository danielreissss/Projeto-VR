using UnityEngine;

/// <summary>
/// Teste rápido — attach em qualquer GameObject, dê Play no Editor.
/// Confirma se o microfone está capturando áudio sem precisar do Quest ou de rede.
/// REMOVA da cena após o teste.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MicTest : MonoBehaviour
{
    private AudioSource _audio;
    private AudioClip   _clip;

    private void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[MicTest] Nenhum microfone encontrado!");
            return;
        }

        string mic = Microphone.devices[0];
        Debug.Log($"[MicTest] Microfone encontrado: '{mic}'");

        _audio      = GetComponent<AudioSource>();
        _clip       = Microphone.Start(mic, true, 5, 24000);
        _audio.clip = _clip;
        _audio.loop = true;

        // Aguarda o mic iniciar e reproduz o que captura (loopback)
        while (Microphone.GetPosition(mic) <= 0) { }
        _audio.Play();

        Debug.Log("[MicTest] Reproduzindo microfone em loopback. Fale e você vai se ouvir!");
    }

    private void Update()
    {
        // Loga volume RMS a cada 60 frames
        if (_clip == null || Time.frameCount % 60 != 0) return;

        float[] data = new float[480];
        _clip.GetData(data, Microphone.GetPosition(null) - 480);

        float rms = 0f;
        foreach (var s in data) rms += s * s;
        rms = Mathf.Sqrt(rms / data.Length);

        Debug.Log($"[MicTest] Volume RMS: {rms:F4} {(rms > 0.01f ? "✔ CAPTANDO" : "— silencioso")}");
    }

    private void OnDestroy()
    {
        if (Microphone.IsRecording(null))
            Microphone.End(null);
    }
}
