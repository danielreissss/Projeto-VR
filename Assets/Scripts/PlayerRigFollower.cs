using UnityEngine;
using Unity.Netcode;

public class PlayerRigFollower : NetworkBehaviour
{
    private Transform cameraRigTransform;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

  
        OVRCameraRig ovrRig = FindFirstObjectByType<OVRCameraRig>();

        if (ovrRig != null)
        {
            cameraRigTransform = ovrRig.transform;
        }
        else
        {
            Debug.LogError("PlayerRigFollower: Não foi possível encontrar a OVRCameraRig na cena!");
        }
    }

    void LateUpdate() // Usar LateUpdate é melhor para seguir objetos de câmera
    {
        if (!IsOwner || cameraRigTransform == null) return;

        transform.position = cameraRigTransform.position;
        float yRotation = cameraRigTransform.rotation.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}