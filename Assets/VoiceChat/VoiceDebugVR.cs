using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Linq;

public class VoiceDebugVR : MonoBehaviour
{
    [Tooltip("Distância que o HUD flutuará na frente da câmera (em metros).")]
    public float distance = 1.5f;
    [Tooltip("Escala geral do HUD holográfico.")]
    public float scale = 0.25f;

    private TextMeshPro _text;
    private VoiceSender _localSender;

    void Start()
    {
        // Debug HUD desabilitado para ganho de performance em Quest 3
        enabled = false;
        return;

        // Cria dinamicamente um objeto 3D TextMeshPro flutuante
        GameObject debugObj = new GameObject("VoiceDebugText_Hologram");
        _text = debugObj.AddComponent<TextMeshPro>();
        _text.fontSize  = 1.5f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color     = Color.white;

        // Outline para legibilidade em qualquer fundo de luz
        _text.outlineWidth = 0.2f;
        _text.outlineColor = Color.black;

        // Anexa na Câmera Principal (âncora CenterEye do VR no Quest)
        if (Camera.main != null)
        {
            debugObj.transform.SetParent(Camera.main.transform);
            debugObj.transform.localPosition = new Vector3(0, 0, distance);
            debugObj.transform.localRotation = Quaternion.identity;
            debugObj.transform.localScale    = Vector3.one * scale;
        }
        else
        {
            Debug.LogWarning("[VoiceDebugVR] Camera.main não encontrada. HUD ficará na origem do mundo.");
        }
    }

    void Update()
    {
        if (_text == null) return;

        string status = "<color=#00FFFF><b>--- HUD DE VOZ (DRIVE E) ---</b></color>\n\n";

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            _text.text = status + "<color=#FF0000>Desconectado da Rede</color>";
            return;
        }

        bool hasPerm = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        hasPerm = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#endif
        status += $"Permissao Mic: {(hasPerm ? "<color=#00FF00>OK</color>" : "<color=#FF0000>NEGADA</color>")}\n";
        status += $"Rede: <color=#FFFF00>{(nm.IsServer ? "Servidor/Host" : "Cliente")}</color>\n";
        
        var allNetObjs = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        
        // Conta apenas objetos que REALMENTE possuem Voz (Sender ou Receiver) e estão ativos
        int playersWithVoice = allNetObjs.Count(n => 
            n.gameObject.activeInHierarchy && 
            (n.GetComponent<VoiceSender>() != null || n.GetComponent<VoiceReceiver>() != null));
        
        status += $"Avatares com Voz: {playersWithVoice}\n";
        
        string[] devices = Microphone.devices;
        status += $"Mics encontrados: {(devices.Length > 0 ? string.Join(", ", devices) : "<color=#FF0000>NENHUM</color>")}\n\n";

        // ── Microfone local ─────────────────────────────────────────
        if (_localSender == null || !_localSender.gameObject.activeInHierarchy)
        {
            _localSender = FindObjectsByType<VoiceSender>(FindObjectsSortMode.None)
                           .FirstOrDefault(s =>
                           {
                               var no = s.GetComponentInParent<NetworkObject>();
                               return no != null && no.IsOwner;
                           });
        }

        if (_localSender != null)
        {
            status += $"Seu Avatar: <color=green>{_localSender.gameObject.name}</color>\n";
            string micStatus = _localSender.IsMicReady ? "<color=green>Gravando</color>" : 
                               (_localSender.HasMicClip ? "<color=yellow>Aguardando Buffer...</color>" : "<color=red>FALHA / NÃO INICIOU</color>");
            status += $"Seu Microfone: <color=green>{_localSender.CurrentMicName}</color> ({micStatus})\n";
            // Ajustei para 200x dentro do método GetVolumeBar
            status += "Seu Volume: " + GetVolumeBar(_localSender.CurrentVolume) + "\n\n";

            if (_localSender.CurrentVolume > 0.0001f)
                Debug.Log($"[VoiceDebugVR] Volume detectado: {_localSender.CurrentVolume:F6}");
        }
        else
        {
            if (nm.IsListening)
            {
                status += "Seu Microfone: <color=orange>Procurando avatar local...</color>\n";
                status += "<color=#FF9900>--- DIAGNÓSTICO GLOBAL DA CENA ---</color>\n";
                
                var globalAvatars = allNetObjs.Where(n => 
                    n.gameObject.name.ToLower().Contains("avatar") || 
                    n.gameObject.name.ToLower().Contains("player") || 
                    n.gameObject.name.ToLower().Contains("meta")).ToList();
                    
                status += $"Avatares globais instanciados: {globalAvatars.Count}\n";
                foreach(var av in globalAvatars)
                {
                    string ownership = av.IsOwner ? "<color=green>Local (IsOwner)</color>" : "<color=orange>Remoto</color>";
                    status += $"- <color=yellow>{av.gameObject.name}</color> | OwnerID: {av.OwnerClientId} | {ownership}\n";
                }

                if (globalAvatars.Count == 1 && !nm.IsServer)
                {
                    status += "<color=red>FALHA CRÍTICA: O Host não spawnou o avatar para o Cliente.</color>\n";
                }
                
                var myObjs = allNetObjs.Where(n => n.IsOwner).ToList();
                
                if (myObjs.Count == 0)
                {
                    status += "<color=red>ERRO 1: Nenhum objeto na rede é seu (IsOwner=false).\nO Cliente não tem posse de nenhum objeto (Falha de Spawn do Netcode).</color>\n";
                }
                else
                {
                    status += $"Você tem {myObjs.Count} objeto(s) na rede.\n";
                    
                    NetworkObject myAvatar = null;
                    if (VoiceManager.Instance != null)
                    {
                        myAvatar = myObjs.FirstOrDefault(n => VoiceManager.Instance.IsPlayerObject(n));
                    }
                    
                    if (myAvatar == null)
                    {
                        status += "<color=red>Nenhum objeto do Client atende aos critérios do VoiceManager. O Avatar real pode não ter spawnado ainda.</color>\n";
                        status += "<color=yellow>Objetos IsOwner ignorados (sendo barrados pelo filtro):</color>\n";
                        foreach (var obj in myObjs)
                        {
                            status += $"- <color=gray>{obj.gameObject.name}</color>\n";
                        }
                    }
                    else
                    {
                        status += $"Analisando: <color=yellow>{myAvatar.name}</color>\n";
                        
                        bool hasSender = myAvatar.GetComponent<VoiceSender>() != null;
                        bool hasReceiver = myAvatar.GetComponent<VoiceReceiver>() != null;
                        
                        status += $"Tem Sender? {(hasSender ? "Sim" : "Não")} | Tem Receiver? {(hasReceiver ? "<color=red>Sim (Problema!)</color>" : "Não")}\n";
                        status += $"Identificação: <color=green>Player Confirmado</color>\n";
                        
                        if (!hasSender)
                        {
                            if (!hasPerm)
                                status += "<color=red>ERRO 3: VoiceSender bloqueado porque o Android NEGOU a permissão de mic.</color>\n";
                            else
                                status += "<color=red>ERRO 4: Permissão OK e Avatar válido, mas VoiceSender não anexado. Verifique os logs do VoiceManager.</color>\n";
                        }
                    }
                }
                status += "<color=#FF9900>-----------------------------</color>\n\n";
            }
            else
            {
                status += "Seu Microfone: <color=red>Rede não iniciada</color>\n\n";
            }
        }

        // ── Outros jogadores ────────────────────────────────────────
        status += "<b>Outros Jogadores (Recebendo Voz):</b>\n";
        int remoteCount = 0;

        if (nm.SpawnManager != null && nm.SpawnManager.SpawnedObjects != null)
        {
            // Copia valores para evitar erro de coleção modificada durante iteração
            var spawnedList = nm.SpawnManager.SpawnedObjects.Values.ToList();
            foreach (var netObj in spawnedList)
            {
                if (netObj == null || netObj.IsOwner) continue;

                var receiver = netObj.GetComponent<VoiceReceiver>();
                if (receiver != null)
                {
                    remoteCount++;
                    status += $"Jogador {netObj.OwnerClientId}: " + GetVolumeBar(receiver.CurrentVolume) + "\n";
                }
            }
        }

        if (remoteCount == 0)
        {
            int others = playersWithVoice - 1; // exclui o próprio jogador
            status += others > 0
                ? "<color=orange>Aguardando VoiceReceiver do(s) outro(s) jogador(es)...</color>"
                : "<color=#888888>Nenhum outro jogador na sala no momento.</color>";
        }

        _text.text = status;
    }

    // Gera uma barra visual baseada na intensidade da voz (RMS)
    private string GetVolumeBar(float rms)
    {
        // Aumentei de 300 para 500 para ser ainda mais sensível
        int bars = Mathf.Clamp(Mathf.RoundToInt(rms * 500f), 0, 20);

        if (bars == 0) return "<color=#555555>[ Silêncio ]</color>";

        string barStr   = new string('|', bars);
        string emptyStr = new string('.', 20 - bars);

        string color = bars > 17 ? "red" : bars > 12 ? "yellow" : "green";
        return $"<color={color}>[{barStr}</color><color=#333333>{emptyStr}]</color>";
    }
}