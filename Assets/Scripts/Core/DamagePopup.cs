using UnityEngine;
using TMPro;

namespace IsometricShooter.Core
{
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI textMesh;
        [SerializeField] private float disappearTimer = 1f;
        [SerializeField] private Vector3 moveSpeed = new Vector3(0, 1f, 0);
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color bodyColor = Color.yellow;
        [SerializeField] private Color headshotColor = Color.red;

        private const float FadeSpeed = 3f;

        private Color textColor;
        private Camera mainCam;

        public void Setup(float damageAmount, DamagePopupType popupType)
        {
            if (textMesh == null)
                textMesh = GetComponentInChildren<TextMeshProUGUI>();

            textMesh.text = Mathf.RoundToInt(damageAmount).ToString();

            switch (popupType)
            {
                case DamagePopupType.Headshot:
                    textColor = headshotColor;
                    textMesh.fontSize = 28;
                    break;
                case DamagePopupType.Body:
                    textColor = bodyColor;
                    textMesh.fontSize = 22;
                    break;
                default:
                    textColor = normalColor;
                    textMesh.fontSize = 18;
                    break;
            }

            textMesh.color = textColor;
        }

        private void Update()
        {
            transform.position += moveSpeed * Time.deltaTime;

            if (mainCam == null)
                mainCam = Camera.main;

            if (mainCam != null)
            {
                transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward,
                                 mainCam.transform.rotation * Vector3.up);
            }

            disappearTimer -= Time.deltaTime;
            if (disappearTimer < 0f)
            {
                textColor.a -= FadeSpeed * Time.deltaTime;
                textMesh.color = textColor;
                if (textColor.a < 0f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    public enum DamagePopupType { Normal, Body, Headshot }
}
