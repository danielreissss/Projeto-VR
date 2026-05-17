using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class ManualConnection : MonoBehaviour
{
    [SerializeField] private GameObject uiParent;
    [SerializeField] private TMP_InputField ipAddressField;
    [SerializeField] private TMP_Text textoLogs;

    private void Awake()
    {
        if (textoLogs != null)
            textoLogs.text = "Aguardando acao...";

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectedOnClient;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnectedOnClient;
    }

    public void StartHost()
    {
        Log("Iniciando Host...");
        NetworkManager.Singleton.StartHost();
        if (uiParent != null) uiParent.SetActive(false);
    }

    public void StartClient()
    {
        if (ipAddressField == null) return;
        
        string ipAddress = ipAddressField.text;
        if (string.IsNullOrEmpty(ipAddress))
        {
            Log("ERRO: Endereco de IP nao pode ser vazio.");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
            transport.SetConnectionData(ipAddress, 7777);

        Log($"Tentando conectar ao Host em {ipAddress}...");
        NetworkManager.Singleton.StartClient();
        if (uiParent != null) uiParent.SetActive(false);
    }

    private void HandleServerStarted()
    {
        Log("Servidor iniciado com sucesso!");
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        Log($"Cliente {clientId} conectado ao servidor.");
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        Log($"Cliente {clientId} desconectado do servidor.");
    }

    private void HandleClientConnectedOnClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
        Log($"Conectado ao Host com sucesso! Meu ID e {clientId}");
    }

    private void Log(string message)
    {
        if (textoLogs != null)
            textoLogs.text = message + "\n" + textoLogs.text;
        
        Debug.Log(message);
    }
}
