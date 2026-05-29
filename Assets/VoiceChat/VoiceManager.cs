using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Meta.XR.Movement.Networking.NGO;

/// <summary>
/// Coloque este componente no mesmo GameObject do NetworkCharacterSpawnerNGO.
/// Aguarda permissão de microfone antes de adicionar VoiceSender.
/// </summary>
public class VoiceManager : MonoBehaviour
{
    [Header("Configuração de Voz")]
    public int    sampleRate  = 24000;
    public int    frameSizeMs = 20;
    public int    bitrate     = 24000;
    public string messageName = "VoicePacket";

    [Header("Detecção de Avatar (fallback)")]
    [Tooltip("Palavras-chave no nome do prefab de avatar para forçar detecção caso faltem componentes.")]
    public string[] avatarNameKeywords = { "NetworkAvatar", "Player", "Avatar", "Retargeter", "Meta" };

    [Header("Permissão de Microfone")]
    [Tooltip("Deixe true se não tiver OVRManager com requestRecordAudioPermissionOnStartup = true.")]
    public bool requestMicPermissionManually = true;

    private bool _permissionGranted = false;
    private bool _handlerRegistered = false;

    // Singleton para garantir que apenas um VoiceManager gerencie as mensagens
    public static VoiceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[VoiceManager] Outra instância detectada. Destruindo esta para evitar duplicidade de handlers.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Scan periódico de fallback — começa em ScanInterval para não disparar no frame 0
    private HashSet<ulong> _trackedObjectIds = new HashSet<ulong>();

    // ─────────────────────────────────────────────────────────────
    private void Start()
    {
        StartCoroutine(InitWithPermission());
        StartCoroutine(PollForLocalAvatar());
        StartCoroutine(ClientSpawnSafetyCheck());
    }

    private IEnumerator ClientSpawnSafetyCheck()
    {
        // Aguarda 5 segundos para o spawn automático da Meta acontecer
        yield return new WaitForSeconds(5.0f);

        var nm = NetworkManager.Singleton;
        // Se após 5s ainda formos um cliente sem avatar local (IsOwner)
        if (nm != null && nm.IsClient && !nm.IsServer)
        {
            bool hasLocalAvatar = nm.SpawnManager.SpawnedObjects.Values.Any(n => n.IsOwner && IsPlayerObject(n));

            if (!hasLocalAvatar)
            {
                Debug.LogWarning("[VoiceManager] Client sem avatar detectado após 5s. Forçando spawn via Building Block...");
                var spawner = FindFirstObjectByType<NetworkCharacterSpawnerNGO>();
                if (spawner != null)
                {
                    spawner.SpawnCharacter();
                }
            }
        }
    }

    private IEnumerator PollForLocalAvatar()
    {
        // Aguarda a rede conectar
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            yield return new WaitForSeconds(0.5f);
        }

        bool localAvatarFound = false;

        while (!localAvatarFound)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null && nm.SpawnManager.SpawnedObjects != null)
            {
                foreach (var kvp in nm.SpawnManager.SpawnedObjects)
                {
                    var netObj = kvp.Value;
                    if (netObj != null && netObj.IsSpawned && netObj.IsOwner && IsPlayerObject(netObj))
                    {
                        TryAddVoiceComponents(netObj);
                        if (netObj.GetComponent<VoiceSender>() != null)
                        {
                            localAvatarFound = true;
                            Debug.Log($"[VoiceManager] Avatar local definitivo encontrado: {netObj.name}. Encerrando polling local.");
                            break;
                        }
                    }
                }
            }

            if (!localAvatarFound)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private void Update()
    {
        var nm = NetworkManager.Singleton;

        // Registra o handler assim que o NetworkManager estiver ouvindo
        if (nm != null && nm.IsListening && !_handlerRegistered)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(messageName, OnGlobalVoicePacketReceived);
            _handlerRegistered = true;
            Debug.Log($"[VoiceManager] Handler global registrado para '{messageName}'. IsServer={nm.IsServer}, IsClient={nm.IsClient}, LocalClientId={nm.LocalClientId}");
        }

        // Polling contínuo de baixo custo para detectar novos objetos assim que nascem (sem race-conditions)
        if (nm != null && nm.SpawnManager != null && nm.SpawnManager.SpawnedObjects != null)
        {
            foreach (var kvp in nm.SpawnManager.SpawnedObjects)
            {
                var netObj = kvp.Value;
                if (netObj == null || !netObj.IsSpawned) continue;

                bool hasSender = netObj.GetComponent<VoiceSender>() != null;
                bool hasReceiver = netObj.GetComponent<VoiceReceiver>() != null;
                bool alreadyConfigured = hasSender || hasReceiver;

                if (!_trackedObjectIds.Contains(kvp.Key) || !alreadyConfigured)
                {
                    if (!_trackedObjectIds.Contains(kvp.Key))
                    {
                        _trackedObjectIds.Add(kvp.Key);
                    }
                    
                    // Como Unity inicializa componentes dinamicamente, aguardamos 1 frame
                    StartCoroutine(WaitAndTryAddComponents(netObj));
                }
            }
        }
    }

    private IEnumerator WaitAndTryAddComponents(NetworkObject netObj)
    {
        yield return null; // Aguarda 1 frame para garantir que os scripts de Avatar da Meta terminaram de anexar
        if (netObj != null)
        {
            TryAddVoiceComponents(netObj);
        }
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            if (_handlerRegistered && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(messageName);
                _handlerRegistered = false;
            }
        }
        _trackedObjectIds.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    // Centraliza o recebimento de TODOS os pacotes de voz da sala
    private void OnGlobalVoicePacketReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong originClientId);

        int dataLen = reader.Length - reader.Position;
        if (dataLen <= 0) return;

        byte[] encoded = new byte[dataLen];
        reader.ReadBytesSafe(ref encoded, dataLen);

        var nm = NetworkManager.Singleton;
        if (nm == null) return;


        // 1. RELAY DO SERVIDOR: Se eu sou o Host/Server, repasso o áudio para os clientes
        if (nm.IsServer)
        {
            using var writer = new FastBufferWriter(8 + dataLen, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(originClientId);
            writer.WriteBytesSafe(encoded, dataLen);

            foreach (ulong targetId in nm.ConnectedClientsIds.ToList())
            {
                // Não reenvia para quem originou, para quem enviou ao servidor, nem para o próprio Host
                if (targetId == nm.LocalClientId || targetId == senderClientId || targetId == originClientId) continue;

                nm.CustomMessagingManager.SendNamedMessage(
                    messageName, targetId, writer, NetworkDelivery.Unreliable);
            }
        }

        // 2. REPRODUÇÃO LOCAL: ignorar eco (não reproduzir a voz do próprio cliente)
        if (originClientId == nm.LocalClientId)
        {
            return;
        }

        bool foundAvatar = false;
        if (nm.SpawnManager != null && nm.SpawnManager.SpawnedObjects != null)
        {
            foreach (var netObj in nm.SpawnManager.SpawnedObjects.Values.ToList())
            {
                if (netObj.OwnerClientId == originClientId)
                {
                    foundAvatar = true;
                    var receiver = netObj.GetComponent<VoiceReceiver>();
                    if (receiver != null)
                    {
                        receiver.ReceivePacket(originClientId, encoded);
                    }
                    else
                    {
                        Debug.LogWarning($"[VoiceManager] [AUTO-REPAIR] Pacote de {originClientId} recebido, avatar '{netObj.name}' encontrado, MAS NÃO TEM VoiceReceiver! Adicionando dinamicamente...");
                        TryAddVoiceComponents(netObj);
                        receiver = netObj.GetComponent<VoiceReceiver>();
                        if (receiver != null)
                        {
                            receiver.ReceivePacket(originClientId, encoded);
                        }
                        else
                        {
                            Debug.LogError($"[VoiceManager] Falha ao adicionar VoiceReceiver dinamicamente ao avatar '{netObj.name}'!");
                        }
                    }
                    break;
                }
            }
        }

        if (!foundAvatar)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (nm.SpawnManager != null && nm.SpawnManager.SpawnedObjects != null)
            {
                foreach (var netObj in nm.SpawnManager.SpawnedObjects.Values)
                {
                    sb.Append($"[{netObj.name}: Owner={netObj.OwnerClientId}, IsPlayer={IsPlayerObject(netObj)}, Active={netObj.gameObject.activeInHierarchy}] ");
                }
            }
            Debug.LogError($"[VoiceManager] Pacote de voz com origem {originClientId} não pôde ser roteado. Avatares na cena: {sb.ToString()}");
        }
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Pede permissão de microfone e só então escaneia os avatares.
    /// No Editor e em plataformas não-Android, pula direto para o scan.
    /// </summary>
    private IEnumerator InitWithPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (requestMicPermissionManually)
        {
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(
                    UnityEngine.Android.Permission.Microphone);

                // Aguarda o usuário responder ao diálogo (até 10 segundos)
                float timeout = 10f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                           UnityEngine.Android.Permission.Microphone) && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }

            _permissionGranted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone);

            if (!_permissionGranted)
            {
                Debug.LogWarning("[VoiceManager] Permissão de microfone NEGADA. Chat de voz desativado.");
                yield break;
            }
        }
        else
        {
            // Confia que o OVRManager já pediu a permissão; aguarda 1 frame
            yield return null;
            _permissionGranted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone);
        }
#else
        // Editor / outras plataformas: sem restrição de permissão
        yield return null;
        _permissionGranted = true;
#endif
        Debug.Log($"[VoiceManager] Permissão de microfone: {_permissionGranted}. Iniciando scan...");
        ScanAllNetworkObjects();
    }

    // ─────────────────────────────────────────────────────────────
    private void ScanAllNetworkObjects()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SpawnManager == null || nm.SpawnManager.SpawnedObjects == null) return;
        
        Debug.Log($"[VoiceManager] Iniciando Varredura Manual...");
        
        foreach (var kvp in nm.SpawnManager.SpawnedObjects)
        {
            TryAddVoiceComponents(kvp.Value);
        }
    }

    // ─────────────────────────────────────────────────────────────
    public bool IsPlayerObject(NetworkObject netObj)
    {
        if (netObj == null) return false;
        
        string objName = netObj.gameObject.name;

        // Regra absoluta de exclusão
        if (objName.Contains("BuildingBlock"))
            return false;

        // Ignora o objeto de cena original (NetworkAvatarMeta SEM "(Clone)" no nome).
        // Apenas clones spawned pelo PlayerPrefab devem receber componentes de voz.
        // Nota: IsSceneObject é unreliable no lado do Client, então usamos o nome.
        if (objName == "NetworkAvatarMeta")
            return false;

        // Ignora o LocalCharacter (modelo instanciado pelo NetworkCharacterHandler, não é um player object independente)
        if (objName == "LocalCharacter")
            return false;

        var movementComp = netObj.GetComponentInChildren<Meta.XR.Movement.Networking.NGO.NetworkCharacterBehaviourNGO>();
        var playerScript = netObj.GetComponentInChildren<PlayerNetworkScript>();
        var retargeterComp = netObj.GetComponentInChildren<Meta.XR.Movement.Networking.NetworkCharacterRetargeter>();

        if (movementComp != null || playerScript != null || retargeterComp != null)
            return true;
            
        string lowerName = objName.ToLower();
        if (lowerName.Contains("avatar") || lowerName.Contains("clone"))
            return true;

        foreach (var keyword in avatarNameKeywords)
        {
            if (lowerName.Contains(keyword.ToLower())) 
                return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    private void TryAddVoiceComponents(NetworkObject netObj)
    {
        if (!IsPlayerObject(netObj))
        {
            return;
        }

        // Se o objeto não estiver ativo, pula (evita clones inativos)
        if (!netObj.gameObject.activeInHierarchy)
            return;

        bool isLocal = netObj.IsOwner;

        if (isLocal)
        {
            // Removida a trava do _permissionGranted aqui, pois o VoiceSender
            // já aguarda a permissão internamente.

            if (netObj.GetComponent<VoiceSender>() == null)
            {
                var sender = netObj.gameObject.AddComponent<VoiceSender>();
                sender.sampleRate  = sampleRate;
                sender.frameSizeMs = frameSizeMs;
                sender.bitrate     = bitrate;
                sender.messageName = messageName;
                Debug.Log($"[VoiceManager] VoiceSender adicionado ao avatar local ({netObj.OwnerClientId})");
            }
        }
        else
        {
            if (netObj.GetComponent<VoiceReceiver>() == null)
            {
                // AudioSource adicionado ANTES do VoiceReceiver (RequireComponent)
                if (netObj.GetComponent<AudioSource>() == null)
                    netObj.gameObject.AddComponent<AudioSource>();

                var receiver = netObj.gameObject.AddComponent<VoiceReceiver>();
                receiver.sampleRate  = sampleRate;
                receiver.frameSizeMs = frameSizeMs;
                receiver.messageName = messageName;

                Debug.Log($"[VoiceManager] VoiceReceiver adicionado ao avatar remoto ({netObj.OwnerClientId})");
            }
        }
    }
}
