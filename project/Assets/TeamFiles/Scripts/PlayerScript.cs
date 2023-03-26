using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.UI;

public class PlayerScript : NetworkBehaviour
{
    public int movementSpeed;
    public int jumpHeight;
    public int lookAtMouseSpeed;
    public int knockbackForce;
    public Camera mainCamera;
    public GameObject projectilePrefab;
    public GameObject myProjectile;
    public CharacterController characterController;
    public float shotCooldown = 0.5f;
    public float shotCooldownTimer = 0.0f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        gameObject.name = "Player"+((float)GetComponent<NetworkObject>().OwnerClientId).ToString();
        if(IsServer){
        }
        if (!IsOwner) return;
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (IsOwner){
            PlayerCamera();
            PlayerLookAtMouse();
            PlayerMovement();
            PlayerJump();
            PlayerShot();
            RaycastForward();
        }
        if (IsServer){
            PlayerGravity();
            PlayerShotServerSide();
        }
    }

    private void RaycastForward(){
        RaycastHit hit;
        Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1f, Color.white);
        if (Physics.Raycast(transform.position, transform.forward, out hit, 1f))
        {
            if(hit.transform.gameObject == null) return;
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            Debug.Log("Ray hit "+hit.transform.gameObject.name);
        }
    }

    private void PlayerGravity(){
        if(characterController.isGrounded == false){
            characterController.Move(new Vector3(0, -9.82f, 0) * Time.deltaTime);
        }
    }

    private void PlayerCamera(){
        mainCamera.gameObject.transform.position = gameObject.transform.position + new Vector3(0, 10, -10);
    }

    private void PlayerLookAtMouse(){
        Plane playerPlane = new Plane(Vector3.up, transform.position);
        if (mainCamera is { })
        {
            Ray ray = mainCamera.ScreenPointToRay (Input.mousePosition);
            float hitdist = 0.0f;
            if (playerPlane.Raycast (ray, out hitdist)) 
            {
                Vector3 targetPoint = ray.GetPoint(hitdist);
                Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
                PlayerLookAtMouseServerRpc(targetRotation);
            }
        }
    }

    [ServerRpc]
    private void PlayerLookAtMouseServerRpc(Quaternion targetRotation)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookAtMouseSpeed * Time.deltaTime);
    }

    private void PlayerMovement(){
        var moveDir = new Vector3(0, 0, 0);
        var w = Input.GetKey(KeyCode.W);
        var s = Input.GetKey(KeyCode.S);
        var a = Input.GetKey(KeyCode.A);
        var d = Input.GetKey(KeyCode.D);

        if (w) moveDir.z = 1;
        else if (s) moveDir.z = -1;
        if (a) moveDir.x = -1;
        else if (d) moveDir.x = 1;

        if (w || s || a || d) PlayerMovementServerRpc(moveDir.normalized);
    }

    [ServerRpc]
    private void PlayerMovementServerRpc(Vector3 moveDir)
    {
        characterController.Move(moveDir * Time.deltaTime * movementSpeed);
    }

    private void PlayerJump(){
        if(characterController.isGrounded == true & Input.GetKeyDown(KeyCode.Space)){
            PlayerJumpServerRpc();
        }
    }

    [ServerRpc]
    private void PlayerJumpServerRpc()
    {
        characterController.Move(new Vector3(0, 1, 0) * jumpHeight);
    }

    private void PlayerShot(){
        if (Input.GetKeyUp(KeyCode.Mouse0) && IsOwner){
            PlayerShotServerRpc();
        }
    }

    [ServerRpc]
    private void PlayerShotServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if(shotCooldownTimer >= shotCooldown){
            myProjectile = Instantiate(projectilePrefab, transform.position + gameObject.transform.forward, transform.rotation);
            myProjectile.GetComponent<NetworkObject>().SpawnWithOwnership(serverRpcParams.Receive.SenderClientId);
            shotCooldownTimer = 0;
        }
    }

    private void PlayerShotServerSide()
    {
        if(shotCooldownTimer < shotCooldown){
            shotCooldownTimer += Time.deltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if(!IsServer) return;
        if (other.gameObject.name == "Projectile(Clone)" && other.GetComponent<NetworkObject>().OwnerClientId != OwnerClientId)
        {
            Vector3 vec3 = gameObject.transform.position - other.transform.position;
            vec3 = new Vector3(vec3.x, 0.0f, vec3.z).normalized * Time.deltaTime * knockbackForce;
            characterController.Move(vec3);
        }
        if (other.gameObject.name == "ResetTrigger")
        {
            characterController.enabled = false;
            transform.position = new Vector3(0, 10, 0);
            characterController.enabled = true;
        }
    }
}