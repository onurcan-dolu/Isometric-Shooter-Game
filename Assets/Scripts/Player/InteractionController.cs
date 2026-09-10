using UnityEngine;
using Mirror;
using TMPro;
using IsometricShooter.Core;

namespace IsometricShooter.Player
{
    public class InteractionController : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask interactLayer;
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        [Header("World Prompt")]
        [SerializeField] private TextMeshProUGUI promptWorld;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] private float promptLerpSpeed = 25f;

        [SerializeField] private Camera playerCamera;
        [SerializeField] private float MaxInteractDistance = 2;

        private NetworkIdentity currentTargetIdentity;
        private IInteractable currentInteractable;

        private const float MaxRayDistance = 50;

private void Update()
        {
            if (!isLocalPlayer) return;

            PlayerController playerController = GetComponent<PlayerController>();
            if (playerController != null && playerController.IsInVehicle)
            {
                currentTargetIdentity = null;
                currentInteractable = null;
                SetPrompt("");
                return;
            }

            if (Health.IsDead(gameObject))
            {
                currentTargetIdentity = null;
                currentInteractable = null;
                return;
            }

            CheckForInteractable();

            if (currentTargetIdentity != null && Input.GetKeyDown(interactKey))
            {
                CmdInteract(currentTargetIdentity);
            }
        }

        private void LateUpdate()
        {
            if (!isLocalPlayer) return;
            if (promptWorld == null) return;

            if (currentTargetIdentity != null)
            {
                Vector3 target = currentTargetIdentity.transform.position + promptOffset;
                promptWorld.transform.position = Vector3.Lerp(promptWorld.transform.position, target, promptLerpSpeed * Time.deltaTime);
                FaceCamera(promptWorld.transform);
                promptWorld.gameObject.SetActive(true);
            }
            else
            {
                promptWorld.gameObject.SetActive(false);
            }
        }

        private void CheckForInteractable()
        {
            currentTargetIdentity = null;
            currentInteractable = null;

            if (playerCamera == null) return;

            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, MaxRayDistance, interactLayer))
            {
                if (Vector3.Distance(hit.point, transform.position) > MaxInteractDistance)
                    return;

                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable == null)
                {
                    interactable = hit.collider.GetComponentInParent<IInteractable>();
                }

                if (interactable != null)
                {
                    Component comp = interactable as Component;
                    if (comp != null)
                    {
                        currentTargetIdentity = comp.GetComponent<NetworkIdentity>();
                        currentInteractable = interactable;

                        if (currentTargetIdentity != null)
                        {
                            SetPrompt(interactable.InteractionPrompt);
                            return;
                        }
                    }
                }
            }

            SetPrompt("");
        }

        private void SetPrompt(string text)
        {
            if (promptWorld == null) return;

            if (string.IsNullOrEmpty(text))
            {
                promptWorld.gameObject.SetActive(false);
            }
            else
            {
                promptWorld.text = text;
            }
        }

        private void FaceCamera(Transform target)
        {
            if (playerCamera == null) return;

            target.rotation = Quaternion.LookRotation(target.position - playerCamera.transform.position);
        }

        [Command(requiresAuthority = true)]
        private void CmdInteract(NetworkIdentity identity)
        {
            if (identity == null) return;

            IInteractable interactable = identity.GetComponent<IInteractable>();
            if (interactable == null)
            {
                interactable = identity.GetComponentInParent<IInteractable>();
            }

if (interactable != null)
            {
                if (Health.IsDead(gameObject)) return;

                float distance = Vector3.Distance(gameObject.transform.position, identity.transform.position);
                if (distance > MaxInteractDistance)
                    return;

                interactable.Interact(gameObject);
            }
        }

        public bool HasTargetInRange() => currentTargetIdentity != null;
        public string GetCurrentPrompt()
        {
            if (currentInteractable != null) return currentInteractable.InteractionPrompt;
            return "";
        }
    }
}