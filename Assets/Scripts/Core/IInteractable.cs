using UnityEngine;

namespace IsometricShooter.Core
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        void Interact(GameObject interactor);
    }
}