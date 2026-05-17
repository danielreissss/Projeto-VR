using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering;

public class ConnectionManager : MonoBehaviour
{
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        HideUi();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        HideUi();
    }

    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        HideUi();
    }



    private void HideUi()
    {
        this.gameObject.SetActive(false);
    }
}
