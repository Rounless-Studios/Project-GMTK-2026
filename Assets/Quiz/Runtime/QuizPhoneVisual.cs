using UnityEngine;

namespace Gmtk2026.Quiz
{
    [DisallowMultipleComponent]
    public sealed class QuizPhoneVisual : MonoBehaviour
    {
        private const string PhonePrefabResourcePath = "FreePhone1k";
        private const float PhoneModelScale = 18f;

        private Material bodyMaterial;
        private Material accentMaterial;

        public void Build()
        {
            if (transform.childCount > 0)
            {
                return;
            }

            GameObject phonePrefab =
                Resources.Load<GameObject>(PhonePrefabResourcePath);
            if (phonePrefab != null)
            {
                GameObject phoneModel = Instantiate(phonePrefab, transform, false);
                phoneModel.name = "FreePhoneModel";
                phoneModel.transform.localPosition = Vector3.zero;
                phoneModel.transform.localRotation = Quaternion.identity;
                phoneModel.transform.localScale = Vector3.one * PhoneModelScale;
                return;
            }

            BuildFallbackVisual();
        }

        private void BuildFallbackVisual()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            bodyMaterial = new Material(shader)
            {
                color = new Color(0.025f, 0.03f, 0.045f, 1f)
            };
            accentMaterial = new Material(shader)
            {
                color = new Color(0.08f, 0.1f, 0.15f, 1f)
            };

            CreatePart("PhoneBack", new Vector3(1.34f, 2.36f, 0.12f), new Vector3(0f, 0f, 0.04f), bodyMaterial);
            CreatePart("FrameLeft", new Vector3(0.08f, 2.28f, 0.10f), new Vector3(-0.63f, 0f, -0.04f), accentMaterial);
            CreatePart("FrameRight", new Vector3(0.08f, 2.28f, 0.10f), new Vector3(0.63f, 0f, -0.04f), accentMaterial);
            CreatePart("FrameTop", new Vector3(1.26f, 0.10f, 0.10f), new Vector3(0f, 1.13f, -0.04f), accentMaterial);
            CreatePart("FrameBottom", new Vector3(1.26f, 0.10f, 0.10f), new Vector3(0f, -1.13f, -0.04f), accentMaterial);
            CreatePart("CameraIsland", new Vector3(0.34f, 0.12f, 0.035f), new Vector3(0f, 1.045f, -0.105f), bodyMaterial);
        }

        private void OnDestroy()
        {
            if (bodyMaterial != null)
            {
                Destroy(bodyMaterial);
            }

            if (accentMaterial != null)
            {
                Destroy(accentMaterial);
            }
        }

        private void CreatePart(
            string partName,
            Vector3 scale,
            Vector3 localPosition,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                Destroy(partCollider);
            }
        }
    }
}
