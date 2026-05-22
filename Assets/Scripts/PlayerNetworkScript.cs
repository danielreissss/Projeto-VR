using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class PlayerNetworkScript : NetworkBehaviour
{
    public float speed = 20.0f;

    // Mantem compatibilidade com o Inspector do Unity usando FormerlySerializedAs
    [FormerlySerializedAs("sapwnedObjectPrefab")]
    [SerializeField] private Transform spawnedObjectPrefab;

    private Transform spawnedObjectTransform;

    public struct MyCustomData : INetworkSerializable
    {
        public int _int;
        public bool _bool;
        public FixedString128Bytes message;

        // Construtor customizado para permitir inicializacao limpa em linha
        public MyCustomData(int integerValue, bool booleanValue, string msg = "")
        {
            _int = integerValue;
            _bool = booleanValue;
            message = msg;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref message);
        }
    }

    // EXPLICACAO DE C#:
    // Inicializadores de campo nao podem usar a sintaxe de inicializador de objeto/struct { _int = 56... }
    // diretamente na declaracao de campos de instancia em algumas versoes do C# do Unity se houver limitacoes de contexto.
    // Para resolver isso, usamos o construtor customizado que criamos acima:
    private MyCustomData _data = new MyCustomData(56, true, "Mensagem Inicial");

    // Correcao do nome randomNumber (U minusculo) e inicializacao segura usando o construtor
    private NetworkVariable<MyCustomData> randomNumber = new NetworkVariable<MyCustomData>(
        new MyCustomData(56, true), 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        randomNumber.OnValueChanged += (MyCustomData previousValue, MyCustomData newValue) =>
        {
            Debug.Log(OwnerClientId + "; " + "randomNumber: " + newValue._int + "; " + newValue._bool + "; " + newValue.message);
        };
    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.V)) 
        {
            randomNumber.Value = new MyCustomData(Random.Range(0, 100), false, "Hello, world!");
        }

        if (Input.GetKeyDown(KeyCode.C)) 
        {
            TestServerRpc("Hello, world!", new ServerRpcParams());
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            // Apenas o Servidor/Host pode chamar TestClientRpc diretamente. O cliente comum deve solicitar ao servidor.
            if (IsServer)
            {
                TestClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { 1 } } });
            }
            
            // Solicita ao servidor para spawnar o objeto em rede de forma segura
            RequestSpawnObjectServerRpc();
        }

        if (Input.GetKeyDown(KeyCode.F)) 
        {
            // Solicita ao servidor para destruir o objeto spawnado em rede de forma segura
            RequestDestroyObjectServerRpc();
        }

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(horizontalInput, 0, verticalInput);
        transform.Translate(direction * Time.deltaTime * speed, Space.World);
    }

    [ServerRpc]
    private void TestServerRpc(string message, ServerRpcParams sRpcParams)
    {
        Debug.Log("Testando " + OwnerClientId + "; " + sRpcParams.Receive.SenderClientId + "; " + message);
    }

    [ClientRpc]
    private void TestClientRpc(ClientRpcParams cRpcParams)
    {
        Debug.Log("Teste ClientRpc");
    }

    [ServerRpc]
    private void RequestSpawnObjectServerRpc()
    {
        if (spawnedObjectPrefab == null) return;

        // Instancia no servidor
        GameObject spawnedObj = Instantiate(spawnedObjectPrefab.gameObject);
        
        // Faz o spawn em rede para que todos os clientes vejam
        NetworkObject netObj = spawnedObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        
        // Armazena a referencia no servidor para poder destruir depois
        spawnedObjectTransform = spawnedObj.transform;
    }

    [ServerRpc]
    private void RequestDestroyObjectServerRpc()
    {
        if (spawnedObjectTransform != null)
        {
            NetworkObject netObj = spawnedObjectTransform.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true); // Despawn destroi o objeto em toda a rede
            }
            else
            {
                Destroy(spawnedObjectTransform.gameObject);
            }
            spawnedObjectTransform = null;
        }
    }
}