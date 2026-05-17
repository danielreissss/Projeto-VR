using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Concentus.Structs;

/// <summary>
/// Recebe pacotes Opus via NGO, decoda e reproduz via AudioSource.
/// Ring buffer sincronizado com timeSamples para evitar silêncio/glitches.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VoiceReceiver : MonoBehaviour
{
    // ── Configuração ──────────────────────────────────────────────
    [Header("Áudio")]
    public int sampleRate  = 24000;
    public int frameSizeMs = 20;

    [Header("Rede")]
    public string messageName = "VoicePacket";

    // Exposição pública do volume da voz recebida para UI de Debug Holográfica
    public float CurrentVolume { get; private set; }

    // ── Privado ────────────────────────────────────────────────────
    private AudioSource  _audioSource;
    private AudioClip    _streamClip;
    private int          _frameSize;
    private int          _writePos;
    private bool         _started = false;

    private readonly Dictionary<ulong, OpusDecoder> _decoders    = new();

    // CORREÇÃO 3: fila protegida por lock — ReceivePacket é chamado da thread de rede
    private readonly Queue<float[]>                 _decodeQueue = new();
    private readonly object                         _queueLock   = new();

    // Buffer de ~1.6 s; margem inicial de ~160 ms para o read pointer não alcançar o write pointer
    private const int BufferFrames = 80;
    private const int LeadFrames   = 8;

    // ─────────────────────────────────────────────────────────────
    private void Start()
    {
        _frameSize   = sampleRate * frameSizeMs / 1000;
        _audioSource = GetComponent<AudioSource>();

        int clipSamples = _frameSize * BufferFrames;
        _streamClip = AudioClip.Create("VoiceStream", clipSamples, 1, sampleRate, false);

        _audioSource.clip         = _streamClip;
        _audioSource.loop         = true;
        _audioSource.volume       = 1f;
        _audioSource.spatialBlend = 1f;   // 3D — mude para 0 se quiser som 2D
        _audioSource.spatialize   = true; // Ativa espacialização de hardware (Oculus Spatializer)
        _audioSource.Play();

        // Write pointer começa com margem na frente do read pointer
        _writePos = LeadFrames * _frameSize;
        _started  = true;
    }

    // ─────────────────────────────────────────────────────────────
    // Chamado pelo VoiceManager após triagem de pacotes.
    // ATENÇÃO: pode ser chamado de qualquer thread — use o lock.
    public void ReceivePacket(ulong originClientId, byte[] encoded)
    {
        // CORREÇÃO 4: guarda o _frameSize localmente para não acessar campo
        // antes de Start() ter sido chamado (race condition no primeiro frame)
        if (!_started) return;

        // SEGURANÇA EXTRA: Se este avatar é o local, nunca deve tocar áudio vindo da rede.
        // Isso resolve o problema de "ouvir a si mesmo" (curto-circuito).
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsOwner)
        {
            return;
        }

        if (!_decoders.TryGetValue(originClientId, out var decoder))
        {
            decoder = new OpusDecoder(sampleRate, 1);
            _decoders[originClientId] = decoder;
            Debug.Log($"[VoiceReceiver] Novo decoder para client {originClientId}");
        }

        short[] pcmShort = new short[_frameSize];
        int decoded = decoder.Decode(encoded, 0, encoded.Length, pcmShort, 0, _frameSize, false);
        if (decoded <= 0) return;

        float[] pcmFloat = new float[decoded];
        for (int i = 0; i < decoded; i++)
            pcmFloat[i] = pcmShort[i] / 32768f;

        lock (_queueLock)
            _decodeQueue.Enqueue(pcmFloat);
    }

    // ─────────────────────────────────────────────────────────────
    private void Update()
    {
        lock (_queueLock)
        {
            if (_decodeQueue.Count == 0)
            {
                // Decai o volume visual quando ninguém está falando
                CurrentVolume = Mathf.Lerp(CurrentVolume, 0f, Time.deltaTime * 2f);
                return;
            }

            float maxRmsThisFrame = 0f;

            while (_decodeQueue.Count > 0)
            {
                float[] frame  = _decodeQueue.Dequeue();
                int     clipLen = _streamClip.samples;
                int     readPos = _audioSource.timeSamples;

                // Detecta se o write pointer está prestes a alcançar o read pointer
                int distance = (_writePos - readPos + clipLen) % clipLen;
                if (distance < _frameSize)
                {
                    // Underrun: avança write pointer com margem de segurança
                    _writePos = (readPos + LeadFrames * _frameSize) % clipLen;
                    Debug.LogWarning("[VoiceReceiver] Buffer underrun — resetando margem.");
                }

                // CORREÇÃO 3b: verifica se o write pointer não vai ultrapassar o fim do clip
                // (SetData com offset + length fora do clip causa crash no Quest)
                int spaceToEnd = clipLen - _writePos;
                if (frame.Length > spaceToEnd)
                {
                    // Escreve em duas partes para fazer o wrap corretamente
                    float[] part1 = new float[spaceToEnd];
                    float[] part2 = new float[frame.Length - spaceToEnd];
                    System.Array.Copy(frame, 0, part1, 0, spaceToEnd);
                    System.Array.Copy(frame, spaceToEnd, part2, 0, part2.Length);
                    _streamClip.SetData(part1, _writePos);
                    _streamClip.SetData(part2, 0);
                }
                else
                {
                    _streamClip.SetData(frame, _writePos);
                }

                _writePos = (_writePos + frame.Length) % clipLen;

                // RMS para a barra de debug
                float rms = 0f;
                for (int i = 0; i < frame.Length; i++) rms += frame[i] * frame[i];
                float frameRms = Mathf.Sqrt(rms / frame.Length);
                if (frameRms > maxRmsThisFrame) maxRmsThisFrame = frameRms;
            }

            // Atualiza a UI de forma suave (pega os picos altos e decai devagar)
            if (maxRmsThisFrame > CurrentVolume)
                CurrentVolume = maxRmsThisFrame;
            else
                CurrentVolume = Mathf.Lerp(CurrentVolume, 0f, Time.deltaTime * 2f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        foreach (var dec in _decoders.Values)
            dec.Dispose();
        _decoders.Clear();
    }
}
