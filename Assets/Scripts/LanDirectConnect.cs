using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class LanDirectConnect : MonoBehaviour
{
    [Header("Configuração de Rede Local")]
    [Tooltip("Deixe VAZIO para ser o HOST. Coloque o IP do Host/Servidor para ser CLIENTE.")]
    public string hostIpAddress = "";
    public ushort port = 7777;

    [Tooltip("Arquivo opcional (em Application.persistentDataPath) com o IP do servidor, para trocar o IP sem refazer o build.")]
    public string ipOverrideFile = "server_ip.txt";

    private enum Mode { Server, Host, Client }

    private void Start()
    {
        // Pequeno atraso para garantir que o NetworkManager já inicializou
        StartCoroutine(SetupConnectionCoroutine());
    }

    private IEnumerator SetupConnectionCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        ReadCommandLine(out Mode? forcedMode, out string argIp, out ushort? argPort);
        if (argPort.HasValue) port = argPort.Value;

        string ip = !string.IsNullOrEmpty(argIp) ? argIp : ReadIpOverrideFile() ?? hostIpAddress;

        Mode mode;
        if (forcedMode.HasValue) mode = forcedMode.Value;
#if UNITY_SERVER
        // Build "Linux Dedicated Server": sem óculos, roda apenas como servidor
        else mode = Mode.Server;
#else
        else if (Application.isBatchMode) mode = Mode.Server;
        else mode = string.IsNullOrEmpty(ip) ? Mode.Host : Mode.Client;
#endif

        switch (mode)
        {
            case Mode.Server:
                Debug.Log($"--- LAN: Iniciando como SERVIDOR DEDICADO na porta {port} ---");
                transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
                NetworkManager.Singleton.StartServer();
                break;
            case Mode.Host:
                Debug.Log("--- LAN: Iniciando como HOST ---");
                // O Host escuta em "0.0.0.0" (todas as interfaces de rede)
                transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
                NetworkManager.Singleton.StartHost();
                break;
            default:
                Debug.Log($"--- LAN: Iniciando como CLIENTE conectando em {ip}:{port} ---");
                transport.SetConnectionData(ip, port);
                NetworkManager.Singleton.StartClient();
                break;
        }
    }

    // Argumentos aceitos: -server | -host | -client, -ip <endereço>, -port <porta>
    private static void ReadCommandLine(out Mode? mode, out string ip, out ushort? port)
    {
        mode = null; ip = null; port = null;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "-server") mode = Mode.Server;
            else if (a == "-host") mode = Mode.Host;
            else if (a == "-client") mode = Mode.Client;
            else if (a == "-ip" && i + 1 < args.Length) ip = args[++i];
            else if (a == "-port" && i + 1 < args.Length && ushort.TryParse(args[++i], out ushort p)) port = p;
        }
    }

    private string ReadIpOverrideFile()
    {
        if (string.IsNullOrEmpty(ipOverrideFile)) return null;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, ipOverrideFile);
            if (!File.Exists(path)) return null;
            string ip = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(ip)) return null;
            Debug.Log($"--- LAN: IP lido de {path}: {ip} ---");
            return ip;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"--- LAN: não foi possível ler {ipOverrideFile}: {e.Message} ---");
            return null;
        }
    }
}
