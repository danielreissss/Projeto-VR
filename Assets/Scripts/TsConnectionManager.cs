using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Meta.XR.Movement.Networking;
using Meta.XR.Movement.Retargeting;

public class TsConnectionManager : NetworkBehaviour
{
    #region Declara Variáveis, headers e etc
    [Header("UI Feedback")]
    [SerializeField] private TMP_Text selectedAvatarText;
    [SerializeField] private Transform avatarPreviewSpot; // Ponto para instanciar o preview

    [Header("Player Prefabs")]
    [SerializeField] private List<GameObject> playerPrefabs;

    [Header("UI Reference")]
    [SerializeField] private GameObject connectionUIParent;

    private int localSelectedPrefabIndex = 0; // Começa com o primeiro avatar selecionado por padrão
    private GameObject currentPreviewObject;
    #endregion

    #region Função Start
    // A função Start é chamada quando o script é habilitado
    private void Start()
    {
        // Garante que temos um avatar selecionado e atualiza a UI assim que o jogo começa
        UpdateAvatarSelectionUI();
    }
    #endregion

    #region Lógica de Seleção de Avatar (Chamada pela UI)

    public void NextAvatar()
    {
        localSelectedPrefabIndex = (localSelectedPrefabIndex +1 ) % playerPrefabs.Count;
        UpdateAvatarSelectionUI();
    }

    public void PreviousAvatar()
    {
        localSelectedPrefabIndex--;
        if (localSelectedPrefabIndex < 0)
        {
            localSelectedPrefabIndex = playerPrefabs.Count - 1; // Vai para o final da lista
        }
        UpdateAvatarSelectionUI();
    }

    private void UpdateAvatarSelectionUI()
    {
        // Garante que a lista não está vazia
        if (playerPrefabs == null || playerPrefabs.Count == 0)
        {
            selectedAvatarText.text = "Nenhum avatar disponível.";
            return;
        }

        // 1. Atualiza o texto
        selectedAvatarText.text = $"Selecionado: {playerPrefabs[localSelectedPrefabIndex].name}";

        // 2. Atualiza o preview 3D (se configurado)
        if (avatarPreviewSpot != null)
        {
            // Destroi o preview anterior, se houver
            if (currentPreviewObject != null)
            {
                Destroy(currentPreviewObject);
            }

            // Instancia o novo preview
            currentPreviewObject = Instantiate(playerPrefabs[localSelectedPrefabIndex], avatarPreviewSpot.position, avatarPreviewSpot.rotation);

            // Desativa componentes de rede/controle no preview para que ele não interfira
            if (currentPreviewObject.GetComponent<NetworkObject>() != null)
                currentPreviewObject.GetComponent<NetworkObject>().enabled = false;
            if (currentPreviewObject.GetComponent<NetworkCharacterRetargeter>() != null)
                currentPreviewObject.GetComponent<NetworkCharacterRetargeter>().enabled = false;
            if(currentPreviewObject.GetComponent<MetaSourceDataProvider>() != null)
                currentPreviewObject.GetComponent<MetaSourceDataProvider>().enabled = false;
            if(currentPreviewObject.GetComponent<Animator>() != null)
                currentPreviewObject.GetComponent<Animator>().enabled = false;

        }
    }

    //Função pra esconder a UI
    private void HideUI()
    {
        if (connectionUIParent != null) { 
            connectionUIParent.SetActive(false); 
        }
        else
        {
            Debug.Log("Não foi possível encontrar a UI que precisa ser apagada");
        }
    }

    #endregion


    // O resto do script (StartHost, StartClient, ConnectionApproval) continua
    // exatamente o mesmo de antes, pois ele já lê a variável 'localSelectedPrefabIndex'.

    #region Fluxo de Configuração e Conexão

    public void StartServer()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback -= ConnectionApproval;
        NetworkManager.Singleton.ConnectionApprovalCallback += ConnectionApproval;
        NetworkManager.Singleton.StartServer();
        HideUI();
    }

    public void StartHost()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback -= ConnectionApproval;
        NetworkManager.Singleton.ConnectionApprovalCallback += ConnectionApproval;
        NetworkManager.Singleton.StartHost();
        HideUI();
    }

    public void StartClient()
    {
        byte[] connectionData = System.Text.Encoding.UTF8.GetBytes(localSelectedPrefabIndex.ToString());
        NetworkManager.Singleton.NetworkConfig.ConnectionData = connectionData;
        NetworkManager.Singleton.StartClient();
        HideUI();
    }
    #endregion

    #region Lógica do Servidor
    private void ConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
        int selectedPrefabIndex = 0;

        // Verificação de segurança para evitar erro se o payload estiver vazio
        if (!string.IsNullOrEmpty(payload))
        {
            selectedPrefabIndex = int.Parse(payload);
        }


        if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
        {
            selectedPrefabIndex = localSelectedPrefabIndex;
        }

        if (selectedPrefabIndex < 0 || selectedPrefabIndex >= playerPrefabs.Count)
        {
            response.Approved = false;
            Debug.Log("Erro na hora de passar o index do avatar");
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = playerPrefabs[selectedPrefabIndex].GetComponent<NetworkObject>().PrefabIdHash;
        response.Position = Vector3.zero;
        response.Rotation = Quaternion.identity;
    }
    #endregion

    #region Cleanup

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= ConnectionApproval;
        }
        base.OnNetworkDespawn();
    }

 }
    #endregion