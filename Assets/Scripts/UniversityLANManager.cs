using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI; // Se você for usar UI simples para o IP

public class UniversityLANManager : MonoBehaviour
{
    [Header("Configurações de Rede Local")]
    [Tooltip("IP do Óculos que será o HOST (ex: 192.168.1.15)")]
    public string targetIP = "127.0.0.1";
    
    [Tooltip("Porta padrão do Unity Netcode")]
    public ushort port = 7777;

    private UnityTransport _transport;

    private void Awake()
    {
        // Pega automaticamente o componente de transporte do NetworkManager
        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    }

    /// <summary>
    /// Inicia o Óculos como Servidor e Jogador ao mesmo tempo.
    /// Em LAN, o Host deve usar o endereço 0.0.0.0 para escutar qualquer entrada.
    /// </summary>
    public void StartHostLAN()
    {
        Debug.Log("Iniciando como HOST (Servidor Local)...");
        _transport.SetConnectionData("0.0.0.0", port);
        NetworkManager.Singleton.StartHost();
    }

    /// <summary>
    /// Inicia o Óculos como Cliente.
    /// Ele tentará se conectar diretamente ao IP definido no campo 'targetIP'.
    /// </summary>
    public void StartClientLAN()
    {
        Debug.Log($"Iniciando como CLIENTE. Tentando conectar em: {targetIP}");
        _transport.SetConnectionData(targetIP, port);
        NetworkManager.Singleton.StartClient();
    }

    /// <summary>
    /// Função utilitária para encerrar a sessão.
    /// </summary>
    public void Shutdown()
    {
        NetworkManager.Singleton.Shutdown();
        Debug.Log("Sessão encerrada.");
    }

    // Opcional: Uma interface simples na tela (Debug) caso você não queira criar botões VR agora
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            GUILayout.Label("IP do Host:");
            targetIP = GUILayout.TextField(targetIP);

            if (GUILayout.Button("INICIAR COMO HOST (Óculos 1)", GUILayout.Height(50))) StartHostLAN();
            if (GUILayout.Button("INICIAR COMO CLIENTE (Óculos 2)", GUILayout.Height(50))) StartClientLAN();
        }
        else
        {
            GUILayout.Label($"Status: {(NetworkManager.Singleton.IsHost ? "Host" : "Cliente")} Ativo");
            if (GUILayout.Button("DESCONECTAR")) Shutdown();
        }
        GUILayout.EndArea();
    }
}