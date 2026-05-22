using System.Collections;
using UnityEngine;

/// <summary>
/// Teste rapido - attach em qualquer GameObject, de Play no Editor.
/// Confirma se o microfone esta capturando audio sem precisar do Quest ou de rede.
/// REMOVA da cena apos o teste.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MicTest : MonoBehaviour
{
    private AudioSource _audio;
    private AudioClip   _clip;

    private IEnumerator Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[MicTest] Nenhum microfone encontrado!");
            yield break;
        }

        string mic = Microphone.devices[0];
        Debug.Log($"[MicTest] Microfone encontrado: '{mic}'");

        _audio      = GetComponent<AudioSource>();
        _clip       = Microphone.Start(mic, true, 5, 24000);
        _audio.clip = _clip;
        _audio.loop = true;

        // Aguarda o mic iniciar de forma assincrona (evita travar a main thread do Unity/Oculus Quest)
        while (Microphone.GetPosition(mic) <= 0) 
        {
            yield return null; 
        }
        
        _audio.Play();
        Debug.Log("[MicTest] Reproduzindo microfone em loopback. Fale e voce vai se ouvir!");
    }

    private void Update()
    {
        // Loga volume RMS a cada 60 frames
        if (_clip == null || Time.frameCount % 60 != 0) return;

        int pos = Microphone.GetPosition(null);
        if (pos < 480) return; // Evita out of bounds se a gravacao acabou de comecar

        float[] data = new float[480];
        _clip.GetData(data, pos - 480);

        float rms = 0f;
        foreach (var s in data) rms += s * s;
        rms = Mathf.Sqrt(rms / data.Length);

        Debug.Log($"[MicTest] Volume RMS: {rms:F4} {(rms > 0.01f ? "CAPTANDO AUDIO" : "Silencioso")}");
    }

    private void OnDestroy()
    {
        if (Microphone.IsRecording(null))
            Microphone.End(null);
    }
}