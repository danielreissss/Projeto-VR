using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;

public class PlayerNetworkScript : NetworkBehaviour
{

    public float speed = 20.0f;

    [SerializeField] private Transform sapwnedObjectPrefab;

    private Transform sapwnedObjectTransform;

    public struct MyCustomData : INetworkSerializable
    {
        public int _int;
        public bool _bool;
        public FixedString128Bytes message;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref message);
            
        }
    }

    //private MyCustomData _data = new MyCustomData { _int = 56, _bool = true, }; --------------- ENTENDER PORQUE ISSO NÃO FUNCIONA ------------------

    private NetworkVariable<MyCustomData> randomNUmber = new NetworkVariable<MyCustomData>( new MyCustomData { _int = 56, _bool = true}, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        randomNUmber.OnValueChanged += (MyCustomData previusValue, MyCustomData newValue) =>
        {
            Debug.Log(OwnerClientId + "; " + "randomNumber: " + newValue._int + "; " + newValue._bool + "; " + newValue.message);
        };

    }

    void Update()
    {

        if(!IsOwner) return;

        /* if (Input.GetKeyDown(KeyCode.V)) {
             randomNUmber.Value = new MyCustomData {_int = Random.Range(0, 100), _bool = false, message = "Hello, world!"};
         }*/

        /*if (Input.GetKeyDown(KeyCode.C)) {
            TestServerRpc("Hello, world!", new ServerRpcParams());
        }*/

        if (Input.GetKeyDown(KeyCode.G))
        {
            TestClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { 1 } } });

            sapwnedObjectTransform = Instantiate(sapwnedObjectPrefab);
            sapwnedObjectTransform.GetComponent<NetworkObject>().Spawn(true);
        }

        if (Input.GetKeyDown(KeyCode.F)) {
            Destroy(sapwnedObjectTransform.gameObject);
        
        }


        float horizantalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(horizantalInput, 0, verticalInput);

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
}
