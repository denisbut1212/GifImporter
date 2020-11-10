using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Example.Script
{
    public class UniGifTest : MonoBehaviour
    {
        [SerializeField] private InputField inputField;
        [SerializeField] private UniGifImage uniGifImage;
        private bool m_mutex;

        public void OnButtonClicked()
        {
            if (m_mutex || uniGifImage == null || string.IsNullOrEmpty(inputField.text)) return;
            m_mutex = true;
            StartCoroutine(ViewGifCoroutine());
        }

        private IEnumerator ViewGifCoroutine()
        {
            yield return StartCoroutine(uniGifImage.SetGifFromUrlCoroutine(inputField.text));
            m_mutex = false;
        }
    }
}