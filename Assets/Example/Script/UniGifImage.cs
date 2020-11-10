using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Example.Script.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Example.Script
{
    /// <summary>
    /// Texture Animation from GIF image
    /// </summary>
    public class UniGifImage : MonoBehaviour
    {
        /// <summary>
        /// This component state
        /// </summary>
        private enum State
        {
            None,
            Loading,
            Ready,
            Playing,
            Pause
        }

        // Target row image
        [SerializeField] private RawImage rawImage;

        // Image Aspect Controller
        [SerializeField] private UniGifImageAspectController imgAspectCtrl;

        // Textures filter mode
        [SerializeField] private FilterMode filterMode = FilterMode.Point;

        // Textures wrap mode
        [SerializeField] private TextureWrapMode wrapMode = TextureWrapMode.Clamp;

        // Load from url on start
        [SerializeField] private bool loadOnStart;

        // GIF image url (WEB or StreamingAssets path)
        [SerializeField] private string loadOnStartUrl;

        // Rotating on loading
        [SerializeField] private bool rotateOnLoading;

        // Debug log flag
        [SerializeField] private bool outputDebugLog;

        // Decoded GIF texture list
        private List<UniGif.GifTexture> m_gifTextureList;

        // Delay time
        private float m_delayTime;

        // Texture index
        private int m_gifTextureIndex;

        // loop counter
        private int m_nowLoopCount;

        /// <summary>
        /// Now state
        /// </summary>
        private State GifState { get; set; }

        /// <summary>
        /// Animation loop count (0 is infinite)
        /// </summary>
        private int LoopCount { get; set; }

        private void Start()
        {
            if (rawImage == null) rawImage = GetComponent<RawImage>();
            if (loadOnStart) SetGifFromUrl(loadOnStartUrl);
        }

        private void OnDestroy() => Clear();

        private void Update()
        {
            switch (GifState)
            {
                case State.None: break;
                case State.Loading:
                    if (rotateOnLoading) transform.Rotate(0f, 0f, 30f * Time.deltaTime, Space.Self);
                    break;
                case State.Ready: break;
                case State.Playing when rawImage == null || m_gifTextureList == null || m_gifTextureList.Count <= 0:
                    return;
                case State.Playing when m_delayTime > Time.time:
                    return;
                case State.Playing:

                    // Change texture
                    m_gifTextureIndex++;
                    if (m_gifTextureIndex >= m_gifTextureList.Count)
                    {
                        m_gifTextureIndex = 0;
                        if (LoopCount > 0)
                        {
                            m_nowLoopCount++;
                            if (m_nowLoopCount >= LoopCount)
                            {
                                Stop();
                                return;
                            }
                        }
                    }

                    rawImage.texture = m_gifTextureList[m_gifTextureIndex].m_texture2d;
                    m_delayTime = Time.time + m_gifTextureList[m_gifTextureIndex].m_delaySec;
                    break;
                case State.Pause: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Set GIF texture from url
        /// </summary>
        /// <param name="url">GIF image url (WEB or StreamingAssets path)</param>
        /// <param name="autoPlay">Auto play after decode</param>
        private void SetGifFromUrl(string url, bool autoPlay = true)
            => StartCoroutine(SetGifFromUrlCoroutine(url, autoPlay));

        /// <summary>
        /// Set GIF texture from url
        /// </summary>
        /// <param name="url">GIF image url (WEB or StreamingAssets path)</param>
        /// <param name="autoPlay">Auto play after decode</param>
        /// <returns>IEnumerator</returns>
        public IEnumerator SetGifFromUrlCoroutine(string url, bool autoPlay = true)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("URL is nothing.");
                yield break;
            }

            if (GifState == State.Loading)
            {
                Debug.LogWarning("Already loading.");
                yield break;
            }

            GifState = State.Loading;
            var path = url.StartsWith("http") ? url : Path.Combine("file:///" + Application.streamingAssetsPath, url);

            // Load file
            using (var www = new WWW(path))
            {
                yield return www;
                if (string.IsNullOrEmpty(www.error) == false)
                {
                    Debug.LogError("File load error.\n" + www.error);
                    GifState = State.None;
                    yield break;
                }

                Clear();
                GifState = State.Loading;

                // Get GIF textures
                yield return StartCoroutine(UniGif.GetTextureListCoroutine(www.bytes,
                    (gifTexList, loopCount, width, height) =>
                    {
                        if (gifTexList != null)
                        {
                            m_gifTextureList = gifTexList;
                            LoopCount = loopCount;
                            GifState = State.Ready;
                            imgAspectCtrl.FixAspectRatio(width, height);
                            if (rotateOnLoading) transform.localEulerAngles = Vector3.zero;
                            if (autoPlay) Play();
                        }
                        else
                        {
                            Debug.LogError("Gif texture get error.");
                            GifState = State.None;
                        }
                    }, filterMode, wrapMode, outputDebugLog));
            }
        }

        /// <summary>
        /// Clear GIF texture
        /// </summary>
        private void Clear()
        {
            if (rawImage != null) rawImage.texture = null;
            if (m_gifTextureList != null)
            {
                for (var i = 0; i < m_gifTextureList.Count; i++)
                    if (m_gifTextureList[i] != null)
                    {
                        if (m_gifTextureList[i].m_texture2d != null)
                        {
                            Destroy(m_gifTextureList[i].m_texture2d);
                            m_gifTextureList[i].m_texture2d = null;
                        }

                        m_gifTextureList[i] = null;
                    }

                m_gifTextureList.Clear();
                m_gifTextureList = null;
            }

            GifState = State.None;
        }

        /// <summary>
        /// Play GIF animation
        /// </summary>
        public void Play()
        {
            if (GifState != State.Ready)
            {
                Debug.LogWarning("State is not READY.");
                return;
            }

            if (rawImage == null || m_gifTextureList == null || m_gifTextureList.Count <= 0)
            {
                Debug.LogError("Raw Image or GIF Texture is nothing.");
                return;
            }

            GifState = State.Playing;
            rawImage.texture = m_gifTextureList[0].m_texture2d;
            m_delayTime = Time.time + m_gifTextureList[0].m_delaySec;
            m_gifTextureIndex = 0;
            m_nowLoopCount = 0;
        }

        /// <summary>
        /// Stop GIF animation
        /// </summary>
        public void Stop()
        {
            if (GifState != State.Playing && GifState != State.Pause)
            {
                Debug.LogWarning("State is not Playing and Pause.");
                return;
            }

            GifState = State.Ready;
        }

        /// <summary>
        /// Pause GIF animation
        /// </summary>
        public void Pause()
        {
            if (GifState != State.Playing)
            {
                Debug.LogWarning("State is not Playing.");
                return;
            }

            GifState = State.Pause;
        }

        /// <summary>
        /// Resume GIF animation
        /// </summary>
        public void Resume()
        {
            if (GifState != State.Pause)
            {
                Debug.LogWarning("State is not Pause.");
                return;
            }

            GifState = State.Playing;
        }
    }
}