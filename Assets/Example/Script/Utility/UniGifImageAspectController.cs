using UnityEngine;

namespace Example.Script.Utility
{
    [ExecuteInEditMode]
    public class UniGifImageAspectController : MonoBehaviour
    {
        public int originalWidth;
        public int originalHeight;
        public bool fixOnUpdate;
        private Vector2 m_lastSize = Vector2.zero;
        private Vector2 m_newSize = Vector2.zero;
        private RectTransform m_rectTransform;

        private RectTransform RectTransform =>
            m_rectTransform != null ? m_rectTransform : m_rectTransform = GetComponent<RectTransform>();

        private void Update()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                FixAspectRatio();
                return;
            }
#endif
            if (fixOnUpdate) FixAspectRatio();
        }

        public void FixAspectRatio(int originalWidth = -1, int originalHeight = -1)
        {
            var forceUpdate = false;
            if (originalWidth > 0 && originalHeight > 0)
            {
                this.originalWidth = originalWidth;
                this.originalHeight = originalHeight;
                forceUpdate = true;
            }

            if (this.originalWidth <= 0 || this.originalHeight <= 0) return;
            bool changeX;
            if (forceUpdate || m_lastSize.x != RectTransform.sizeDelta.x) changeX = true;
            else if (m_lastSize.y != RectTransform.sizeDelta.y) changeX = false;
            else return;
            if (changeX)
            {
                var ratio = RectTransform.sizeDelta.x / this.originalWidth;
                m_newSize.Set(RectTransform.sizeDelta.x, this.originalHeight * ratio);
            }
            else
            {
                var ratio = RectTransform.sizeDelta.y / this.originalHeight;
                m_newSize.Set(this.originalWidth * ratio, RectTransform.sizeDelta.y);
            }

            RectTransform.sizeDelta = m_newSize;
            m_lastSize = RectTransform.sizeDelta;
        }
    }
}