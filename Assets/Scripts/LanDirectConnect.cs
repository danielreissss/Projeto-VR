using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Collections;

public class LanDirectConnect : MonoBehaviour
{
    [Header("Configuração de Rede Local")]
    [Tooltip("Deixe VAZIO para ser o HOST. Coloque o IP do Host para ser CLIENTE.")]
    public string hostIpAddress = ""; 
    public ushort port = 7777;

    private void Start()
    {
        // Pequeno atraso para garantir que o NetworkManager já inicializou
        StartCoroutine(SetupConnectionCoroutine());
    }

    private IEnumerator SetupConnectionCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (string.IsNullOrEmpty(hostIpAddress))
        {
            Debug.Log("--- LAN: Iniciando como HOST ---");
            // O Host escuta em "0.0.0.0" (todas as interfaces de rede do óculos)
            transport.SetConnectionData("0.0.0.0", port);
            NetworkManager.Singleton.StartHost();
        }
        else
        {
            Debug.Log($"--- LAN: Iniciando como CLIENTE conectando em {hostIpAddress} ---");
            // O Cliente aponta para o IP real do outro óculos
            transport.SetConnectionData(hostIpAddress, port);
            NetworkManager.Singleton.StartClient();
        }
    }
}