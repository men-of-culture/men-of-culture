using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerScript : NetworkBehaviour
{

    public int movementSpeed;
    public int jumpHeight;
    public int lookAtMouseSpeed;
    public int knockbackForce;
    public int health;
    public GameObject projectile;
    public Camera mainCamera;
    public CharacterController characterController;

    void Start()
    {
        if (IsOwner) {
            mainCamera = Camera.main;
        }

        if (!IsServer) {
            return;
        }
    }

    void Update()
    {
        if (IsOwner) {
            PlayerCamera();
            PlayerLookAtMouse();
            PlayerMovement();
            PlayerShoot();
        }

        if (!IsServer) return;
    }
    
    private void PlayerCamera() 
    {
        mainCamera.gameObject.transform.position = gameObject.transform.position + new Vector3(0, 10, -10);
    }

    private void PlayerMovement() 
    {
        var moveDir = new Vector3(0, 0, 0);

        if (Input.GetKey(KeyCode.W)) moveDir.z += 1;
        if (Input.GetKey(KeyCode.A)) moveDir.x += -1;
        if (Input.GetKey(KeyCode.S)) moveDir.z += -1;
        if (Input.GetKey(KeyCode.D)) moveDir.x += 1;

        if(moveDir == new Vector3(0, 0, 0)) return;

        PlayerMovementServerRpc(moveDir);

    } 

     [ServerRpc]
    private void PlayerMovementServerRpc(Vector3 moveDir)
    {
        characterController.Move(moveDir.normalized * Time.deltaTime * movementSpeed);
    }

        private void PlayerLookAtMouse()
    {
        if (mainCamera is not { }) return;

        Plane playerPlane = new Plane(Vector3.up, transform.position);
        Ray ray = mainCamera.ScreenPointToRay (Input.mousePosition);
        float hitdist = 0.0f;

        if (!playerPlane.Raycast (ray, out hitdist)) return;

        Vector3 targetPoint = ray.GetPoint(hitdist);
        Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
        PlayerLookAtMouseServerRpc(targetRotation);
    }

    [ServerRpc]
    private void PlayerLookAtMouseServerRpc(Quaternion targetRotation)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookAtMouseSpeed * Time.deltaTime);
    }

    private void PlayerShoot()
    {
        if (Input.GetKeyUp(KeyCode.Mouse0) && IsOwner){
            PlayerShootServerRpc();
        }
    }

    [ServerRpc]
    private void PlayerShootServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var myProjectile = Instantiate(projectile, transform.position + gameObject.transform.forward, transform.rotation);
        myProjectile.GetComponent<NetworkObject>().SpawnWithOwnership(serverRpcParams.Receive.SenderClientId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!IsServer) return;
        if (other.gameObject.tag == "Lava") 
        {
            health += -1;
            if (health == 0) {transform.position = new Vector3(0, 2, 0);}
        }

        if (other.gameObject.name == "Projectile(Clone)" && other.GetComponent<NetworkObject>().OwnerClientId != OwnerClientId)
        {
            Vector3 vec3 = (gameObject.transform.position - other.transform.position) + (other.gameObject.transform.forward);
            vec3 = new Vector3(vec3.x, 0.0f, vec3.z).normalized * Time.deltaTime * knockbackForce;
            characterController.Move(vec3);
        }
    }
}
