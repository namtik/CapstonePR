
using UnityEngine;
using UnityEngine.UI;

namespace Sashindo.VFX
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class AwakeningComboParticleGraphic : MaskableGraphic
    {
        private Camera stageCamera;
        private Transform particleOrigin;
        private ParticleSystem.Particle[] particles;
        private int count;

        internal void Show(Camera camera, Transform origin, ParticleSystem.Particle[] frame, int particleCount)
        {
            stageCamera = camera;
            particleOrigin = origin;
            particles = frame;
            count = particleCount;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (stageCamera == null || particleOrigin == null || canvas == null || particles == null) return;

            Canvas root = canvas.rootCanvas;
            Camera canvasCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector3 cameraRight = stageCamera.transform.right;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = particles[i];
                if (particle.startColor.a == 0) continue;
                Vector3 world = particleOrigin.TransformPoint(particle.position);
                Vector3 point = stageCamera.WorldToScreenPoint(world);
                if (point.z <= stageCamera.nearClipPlane) continue;

                Vector3 side = stageCamera.WorldToScreenPoint(world + cameraRight * particle.startSize * .5f);
                float radius = Mathf.Abs(side.x - point.x);
                if (radius < .1f) continue;
                var extent = new Vector2(radius, radius);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, (Vector2)point - extent,
                        canvasCamera, out Vector2 min) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, (Vector2)point + extent,
                        canvasCamera, out Vector2 max))
                    continue;

                int index = mesh.currentVertCount;
                Color32 color = particle.startColor;
                mesh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
                mesh.AddVert(new Vector3(min.x, max.y), color, Vector2.up);
                mesh.AddVert(new Vector3(max.x, max.y), color, Vector2.one);
                mesh.AddVert(new Vector3(max.x, min.y), color, Vector2.right);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
