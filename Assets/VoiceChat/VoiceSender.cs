using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Concentus.Structs;

/// <summary>
/// Captura microfone, encoda com Opus e envia via NGO para todos os clientes.
/// Aguarda permissão de microfone antes de iniciar (crítico no Quest 3).
/// </summary>
public class VoiceSender : MonoBehaviour
{
    // ── Configuração ──────────────────────────────────────────────
    [Header("Áudio")]
    [Tooltip("Taxa de amostragem. Opus suporta 8000, 12000, 16000, 24000, 48000.")]
    public int sampleRate = 24000;

    [Tooltip("Frames de 20ms são o ponto ideal latência/qualidade para Opus.")]
    public int frameSizeMs = 20;

    [Tooltip("Bitrate alvo em bps. 24000 é bom para voz em VR.")]
    public int bitrate = 24000;

    [Tooltip("Ganho extra na captação (1.0 = normal, 2.0 = dobro).")]
    public float micGain = 1.0f;

    [Header("Rede")]
    [Tooltip("Nome da mensagem NGO. Deve ser igual no VoiceReceiver.")]
    public string messageName = "VoicePacket";

    [Header("Diagnóstico")]
    [Tooltip("Loga o volume RMS do microfone no Console para verificar se está captando áudio.")]
    public bool debugMicVolume = false;

    // NOVO: Exposição pública do volume RMS para a UI de Debug Holográfica
    public float CurrentVolume { get; private set; }

    // NOVO: Nome do microfone sendo utilizado para exibir no Debug
    public string CurrentMicName => string.IsNullOrEmpty(_micDevice) ? "Padrão do Sistema" : _micDevice;

    public bool IsMicReady => _micReady;
    public bool HasMicClip => _micClip != null;

    // ── Privado ────────────────────────────────────────────────────
    private AudioClip   _micClip;
    private string      _micDevice;   // CORREÇÃO: guarda o nome do dispositivo
    private int         _lastSamplePos;
    private int         _frameSize;
    private float[]     _pcmBuffer;
    private short[]     _pcmShort;
    private byte[]      _encodedBuffer;
    private OpusEncoder _encoder;

    private readonly Queue<byte[]> _sendQueue = new();
    private bool _isOwner;
    private bool _micReady = false;

    // ─────────────────────────────────────────────────────────────
    private void Start()
    {
        var netObj = GetComponent<NetworkObject>();
        _isOwner = netObj != null && netObj.IsOwner;
        
        Debug.Log($"[VoiceSender] Componente inicializado no objeto '{gameObject.name}'. IsOwner: {_isOwner}. NetworkObject: {(netObj != null ? "Sim" : "Não")}");

        if (!_isOwner) return;

        // CORREÇÃO: não inicia microfone direto — aguarda permissão
        StartCoroutine(InitMicWithPermissionCheck());
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Aguarda permissão de microfone (Android/Quest) e só então inicializa.
    /// No Editor, pula direto para a inicialização.
    /// </summary>
    private IEnumerator InitMicWithPermissionCheck()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Aguarda até 10 segundos pela permissão
        float timeout = 10f;
        while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                   UnityEngine.Android.Permission.Microphone) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone))
        {
            Debug.LogError("[VoiceSender] Permissão de microfone NEGADA. Voz desativada.");
            yield break;
        }

        // NOSSA CORREÇÃO — polling até o dispositivo aparecer (até 5s)
        float waitDevices = 5f;
        while (Microphone.devices.Length == 0 && waitDevices > 0f)
        {
            waitDevices -= Time.deltaTime;
            yield return null;
        }
#else
        yield return null;
#endif

        InitMic();
    }

    // ─────────────────────────────────────────────────────────────
    private void InitMic()
    {
        // CORREÇÃO: verifica se há dispositivo disponível antes de iniciar
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[VoiceSender] Nenhum microfone encontrado no dispositivo!");
            return;
        }

        // CORREÇÃO DEFINITIVA: 
        // No PC do usuário, 'null' quebrou e 'devices[0]' funcionou.
        // No Quest 3 (Android), 'devices[0]' está retornando SILÊNCIO (provavelmente pegando o canal 
        // virtual de cancelamento de eco da Meta). Lá, precisamos passar 'null' para forçar o mic padrão!
#if UNITY_ANDROID && !UNITY_EDITOR
        string micDevice = null;
#else
        string micDevice = Microphone.devices[0];
#endif
        
        _micDevice = micDevice;   // CORREÇÃO: persiste para usar no Update
        
        Microphone.GetDeviceCaps(micDevice, out int minFreq, out int maxFreq);
        Debug.Log($"[VoiceSender] Usando microfone: '{micDevice}'. Caps: {minFreq}-{maxFreq}Hz. Configurado para: {sampleRate}Hz.");

        _frameSize     = sampleRate * frameSizeMs / 1000;  // ex: 480 samples @ 24kHz
        _pcmBuffer     = new float[_frameSize];
        _pcmShort      = new short[_frameSize];
        _encodedBuffer = new byte[4000];

        _encoder = new OpusEncoder(sampleRate, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = bitrate;
        _encoder.UseVBR  = true;

        // CORREÇÃO: passa o nome do dispositivo explicitamente (null pode falhar no Quest)
        _micClip       = Microphone.Start(micDevice, true, 5, sampleRate);
        _lastSamplePos = 0;

        // CORREÇÃO: valida que o clip foi criado corretamente
        if (_micClip == null)
        {
            Debug.LogError("[VoiceSender] Microphone.Start() retornou null! Verifique permissões.");
            return;
        }

        // CORREÇÃO: aguarda o microfone começar a gravar de fato antes de ler dados
        StartCoroutine(WaitForMicToStart(micDevice));
    }

    private IEnumerator WaitForMicToStart(string micDevice)
    {
        // Microphone.GetPosition retorna -1 enquanto não está gravando
        float timeout = 5f;
        while (Microphone.GetPosition(micDevice) < 0 && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (Microphone.GetPosition(micDevice) < 0)
        {
            Debug.LogError("[VoiceSender] Microfone não iniciou a tempo. Abortando.");
            yield break;
        }

        _micReady = true;
        Debug.Log("[VoiceSender] Microfone pronto e gravando!");
    }

    // ─────────────────────────────────────────────────────────────
    private void Update()
    {
        // CORREÇÃO: só processa depois que o microfone estiver realmente pronto
        if (!_isOwner || !_micReady || _micClip == null) return;

        // CORREÇÃO: usa o nome real do dispositivo, não null (null pode falhar no Quest)
        int currentPos = Microphone.GetPosition(_micDevice);
        if (currentPos < 0) return;

        int available = currentPos >= _lastSamplePos
            ? currentPos - _lastSamplePos
            : (_micClip.samples - _lastSamplePos) + currentPos;

        float maxRmsThisFrame = 0f;

        while (available >= _frameSize)
        {
            _micClip.GetData(_pcmBuffer, _lastSamplePos);
            _lastSamplePos = (_lastSamplePos + _frameSize) % _micClip.samples;
            available -= _frameSize;

            // ── ANÁLISE DE PICO DINÂMICO (NOVO) ──
            float maxPeak = 0f;
            for (int i = 0; i < _frameSize; i++)
            {
                float absSample = Mathf.Abs(_pcmBuffer[i] * micGain);
                if (absSample > maxPeak) maxPeak = absSample;
            }
            // Se o pico estourar o limite digital de 1.0, calcula a atenuação necessária para achatar o estalo
            float attenuator = (maxPeak > 1.0f) ? (1.0f / maxPeak) : 1.0f;

            // Calculamos o RMS aplicando o micGain e o atenuador protetor
            float rms = 0f;
            for (int i = 0; i < _frameSize; i++) 
            {
                float amplified = _pcmBuffer[i] * micGain * attenuator;
                rms += amplified * amplified;
            }
            rms = Mathf.Sqrt(rms / _frameSize);
            
            if (rms > maxRmsThisFrame) maxRmsThisFrame = rms;

            if (debugMicVolume && rms > 0.001f)
            {
                Debug.Log($"[VoiceSender] Volume RMS: {rms:F4}");
            }

            // float → short PCM 16-bit aplicando ganho e a atenuação anti-estalo
            for (int i = 0; i < _frameSize; i++)
                _pcmShort[i] = (short)Mathf.Clamp(_pcmBuffer[i] * 32767f * micGain * attenuator, short.MinValue, short.MaxValue);

            int encodedLen = _encoder.Encode(_pcmShort, 0, _frameSize, _encodedBuffer, 0, _encodedBuffer.Length);
            if (encodedLen <= 0) continue;

            byte[] packet = new byte[encodedLen];
            Buffer.BlockCopy(_encodedBuffer, 0, packet, 0, encodedLen);
            _sendQueue.Enqueue(packet);
        }

        // Atualiza a UI de forma suave (pega os picos altos e decai bem devagar para não piscar no zero)
        if (maxRmsThisFrame > CurrentVolume)
            CurrentVolume = maxRmsThisFrame;
        else
            CurrentVolume = Mathf.Lerp(CurrentVolume, 0f, Time.deltaTime * 2f);

        SendQueuedPackets();
    }

    // ─────────────────────────────────────────────────────────────
    private void SendQueuedPackets()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // CORREÇÃO: verificação mais robusta de conexão
        if (!nm.IsConnectedClient && !nm.IsHost && !nm.IsServer) return;

        ulong localId = nm.LocalClientId;

        while (_sendQueue.Count > 0)
        {
            byte[] packet = _sendQueue.Dequeue();

            using var writer = new FastBufferWriter(8 + packet.Length, Allocator.Temp);
            writer.WriteValueSafe(localId);
            writer.WriteBytesSafe(packet, packet.Length);

            if (nm.IsServer)
            {
                // O Host não pode mandar mensagem para si mesmo no NGO (é ignorado).
                // Portanto, o Host envia diretamente a todos os clientes conectados.
                foreach (ulong targetId in nm.ConnectedClientsIds)
                {
                    if (targetId == localId) continue;
                    nm.CustomMessagingManager.SendNamedMessage(
                        messageName, targetId, writer, NetworkDelivery.Unreliable);
                }
            }
            else
            {
                // Cliente comum envia para o servidor fazer o relay
                nm.CustomMessagingManager.SendNamedMessage(
                    messageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        // CORREÇÃO: usa nome real do dispositivo para encerrar corretamente
        if (_isOwner && _micClip != null && Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);

        _encoder?.Dispose();
    }
}