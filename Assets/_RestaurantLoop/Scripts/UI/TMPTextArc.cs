using TMPro;
using UnityEngine;

namespace RestaurantLoop.UI
{
    /// <summary>
    /// Bends a TextMeshPro label along a symmetric arc by moving and rotating
    /// each rendered glyph. A positive curve height raises the center.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class TMPTextArc : MonoBehaviour
    {
        [SerializeField, Tooltip("How far the center of the label is raised above its edges.")]
        private float curveHeight = 14f;

        [SerializeField, Range(0f, 30f), Tooltip("Maximum clockwise/counter-clockwise tilt at the label edges.")]
        private float edgeRotation = 7f;

        private TextMeshProUGUI textComponent;
        private bool needsArc = true;

        private void Awake()
        {
            CacheTextComponent();
        }

        private void OnEnable()
        {
            CacheTextComponent();
            needsArc = true;
        }

        private void LateUpdate()
        {
            if (textComponent == null || (!needsArc && !textComponent.havePropertiesChanged)) return;

            ApplyArc();
            needsArc = false;
        }

        private void OnValidate()
        {
            CacheTextComponent();
            needsArc = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            needsArc = true;
        }

        private void CacheTextComponent()
        {
            if (textComponent == null)
            {
                textComponent = GetComponent<TextMeshProUGUI>();
            }
        }

        private void ApplyArc()
        {
            textComponent.ForceMeshUpdate();
            TMP_TextInfo textInfo = textComponent.textInfo;
            if (textInfo.characterCount == 0) return;

            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;

                left = Mathf.Min(left, character.bottomLeft.x);
                right = Mathf.Max(right, character.topRight.x);
            }

            float halfWidth = (right - left) * 0.5f;
            if (float.IsInfinity(left) || halfWidth <= Mathf.Epsilon) return;

            float center = (left + right) * 0.5f;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;

                TMP_MeshInfo meshInfo = textInfo.meshInfo[character.materialReferenceIndex];
                int vertexIndex = character.vertexIndex;
                Vector3[] vertices = meshInfo.vertices;
                float glyphCenterX = (vertices[vertexIndex].x + vertices[vertexIndex + 2].x) * 0.5f;
                float normalizedX = Mathf.Clamp((glyphCenterX - center) / halfWidth, -1f, 1f);
                float verticalOffset = curveHeight * (1f - normalizedX * normalizedX);
                float rotation = -normalizedX * edgeRotation;
                Vector3 pivot = new Vector3(glyphCenterX, (vertices[vertexIndex].y + vertices[vertexIndex + 2].y) * 0.5f);
                Quaternion glyphRotation = Quaternion.Euler(0f, 0f, rotation);

                for (int vertex = 0; vertex < 4; vertex++)
                {
                    int index = vertexIndex + vertex;
                    vertices[index] = glyphRotation * (vertices[index] - pivot) + pivot + Vector3.up * verticalOffset;
                }
            }

            textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
