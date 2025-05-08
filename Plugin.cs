using BepInEx;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Valve.VR;

namespace GorillaSource
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        public bool isSource;
        float isSourceTime;

        void Start()
        {
            instance = this;
            HarmonyPatches.ApplyHarmonyPatches();
        }

        StrafeMovement strafe;
        KeyCode keyTarget = KeyCode.G;
        bool lastKeyHit;
        static bool bgmEnabled = true;
        static bool networkedBGM = true;
        static bool networkedMove = true;
        static bool headRotation;
        static bool drawCursor = true;
        Texture2D goldsrc;
        Texture2D cursor;
        void OnGUI()
        {
            if (isSource)
            {
                if (drawCursor)
                {
                    if (cursor == null)
                    {
                        cursor = new Texture2D(1, 1);
                        cursor.SetPixel(1, 1, Color.white);
                        cursor.Apply();
                    }

                    float centerX = Screen.width / 2f;
                    float centerY = Screen.height / 2f;

                    GUI.DrawTexture(new Rect(centerX - 2f, centerY, 5f, 1f), cursor);
                    GUI.DrawTexture(new Rect(centerX, centerY - 2f, 1f, 5f), cursor);
                }

                float timeSinceSource = Time.time - isSourceTime;

                if (timeSinceSource <= 8f)
                {
                    if (goldsrc == null)
                        goldsrc = LoadTextureFromResource("GorillaSource.Resources.goldsrc.png");

                    float alpha = Mathf.Clamp01(1f - (timeSinceSource / 8f));
                    GUI.color = new Color(1f, 1f, 1f, alpha);

                    float iconX = (Screen.width - goldsrc.width) / 2f;
                    float iconY = (Screen.height - goldsrc.height) / 2f;
                    GUI.DrawTexture(new Rect(iconX, iconY, goldsrc.width, goldsrc.height), goldsrc);

                    GUIStyle centeredStyle = new GUIStyle(GUI.skin.label);
                    centeredStyle.alignment = TextAnchor.UpperCenter;
                    centeredStyle.fontSize = 16;
                    centeredStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);

                    string creditText = "GorillaSource, created by goldentrophy\nStrafe movement provided by TheAsuro";
                    Vector2 textSize = centeredStyle.CalcSize(new GUIContent("Strafe movement provided by TheAsuro")); // Longest line for width

                    GUI.Label(
                        new Rect((Screen.width - textSize.x) / 2f, iconY + goldsrc.height + 10f, textSize.x, 40f),
                        creditText,
                        centeredStyle
                    );

                    GUI.color = Color.white;
                }

                if (timeSinceSource > 1f && UnityInput.Current.GetKey(keyTarget))
                {
                    strafe.maxSpeed = GUI.HorizontalSlider(new Rect(10f, 10f, 200f, 15f), strafe.maxSpeed, 0f, 15f);
                    GUI.Label(new Rect(220f, 10f, 800f, 25f), "Ground Speed: " + string.Format("{0:F1}", strafe.maxSpeed));

                    strafe.accel = GUI.HorizontalSlider(new Rect(10f, 40f, 200f, 15f), strafe.accel, 0f, 1000f);
                    GUI.Label(new Rect(220f, 40f, 800f, 25f), "Ground Acceleration: " + strafe.accel.ToString("F0"));

                    strafe.airAccel = GUI.HorizontalSlider(new Rect(10f, 55f, 200f, 15f), strafe.airAccel, 0f, 1000f);
                    GUI.Label(new Rect(220f, 55f, 800f, 25f), "Air Acceleration: " + strafe.airAccel.ToString("F0"));

                    sensitivity = GUI.HorizontalSlider(new Rect(10f, 85f, 200f, 15f), sensitivity, 0f, 1f);
                    GUI.Label(new Rect(220f, 85f, 800f, 25f), "Sensitivity: " + (sensitivity * 100).ToString("F0"));

                    bgmEnabled = GUI.Toggle(new Rect(10f, 100f, 200f, 15f), bgmEnabled, "BGM");
                    networkedBGM = GUI.Toggle(new Rect(10f, 115f, 200f, 15f), networkedBGM, "Networked BGM");

                    headRotation = GUI.Toggle(new Rect(10f, 145f, 200f, 15f), headRotation, "Head Rotation");

                    drawCursor = GUI.Toggle(new Rect(10f, 175f, 200f, 15f), drawCursor, "Crosshair");
                }
            }
            else
            {
                if (UnityInput.Current.GetKey(keyTarget) && !lastKeyHit)
                {
                    isSource = true;
                    isSourceTime = Time.time;

                    strafe = GorillaLocomotion.GTPlayer.Instance.AddComponent<StrafeMovement>();
                    strafe.camObj = GorillaLocomotion.GTPlayer.Instance.bodyCollider.gameObject;
                    Play2DAudio(LoadSoundFromResource("GorillaSource.Resources.valve.wav"), 1f);
                }

                lastKeyHit = UnityInput.Current.GetKey(keyTarget);
            }
        }

        bool cursorLocked = true;
        bool lastCursorKeyHit;
        bool IsSteam;
        bool hasInit;
        float sensitivity = 0.333f;
        public static float rotX;
        public static float rotY;

        void Update()
        {
            if (!isSource)
                return;

            if (!hasInit)
            {
                IsSteam = Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue().ToString().ToLower() == "steam";
                hasInit = true;
            }

            if (UnityInput.Current.GetKey(KeyCode.C) && !lastCursorKeyHit)
                cursorLocked = !cursorLocked;

            lastCursorKeyHit = UnityInput.Current.GetKey(KeyCode.C);

            if (cursorLocked && !XRSettings.isDeviceActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (headRotation)
                GorillaTagger.Instance.offlineVRRig.head.rigTarget.rotation = GorillaTagger.Instance.mainCamera.transform.rotation;

            if (((Mouse.current.rightButton.isPressed && !cursorLocked) || cursorLocked) && !XRSettings.isDeviceActive)
            {
                Transform parentTransform = GorillaLocomotion.GTPlayer.Instance.rightControllerTransform.parent;

                Vector2 delta = Mouse.current.delta.ReadValue();

                rotX -= delta.y * sensitivity;
                rotY += delta.x * sensitivity;

                rotX = Mathf.Clamp(rotX, -90f, 90f);

                parentTransform.rotation = Quaternion.Euler(rotX, rotY, 0f);
            }
            string targetFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\GorillaSource";
            string targetFile = Path.Combine(targetFolder, "bg.wav");
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            if (!File.Exists(targetFile))
            {
                using (WebClient client = new WebClient())
                {
                    // if you really want to you can just selfhost the file, instead of using my domain
                    client.DownloadFile("https://gorillasource.frogiee1.com/bg.wav", targetFile);
                }
            }
        }

        public Vector2 GetLeftJoystickAxis()
        {
            if (IsSteam)
                return SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.GetAxis(SteamVR_Input_Sources.LeftHand);
            else
            {
                Vector2 leftJoystick;
                ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftJoystick);
                return leftJoystick;
            }
        }

        // Thank the god ChatGPT for the WAV formula
        public static AudioClip LoadWav(byte[] wavFile)
        {
            int channels = BitConverter.ToInt16(wavFile, 22);
            int sampleRate = BitConverter.ToInt32(wavFile, 24);

            // Find the "data" chunk
            int dataChunkOffset = 12;
            int dataSize = 0;
            while (dataChunkOffset < wavFile.Length)
            {
                string chunkID = System.Text.Encoding.ASCII.GetString(wavFile, dataChunkOffset, 4);
                int chunkSize = BitConverter.ToInt32(wavFile, dataChunkOffset + 4);

                if (chunkID == "data")
                {
                    dataSize = chunkSize;
                    dataChunkOffset += 8;
                    break;
                }
                dataChunkOffset += 8 + chunkSize;
            }

            int sampleCount = dataSize / 2; // 16-bit PCM = 2 bytes per sample
            float[] audioData = new float[sampleCount];

            int offset = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(wavFile, dataChunkOffset + i * 2);
                audioData[offset++] = sample / 32768f;
            }

            AudioClip clip = AudioClip.Create("EmbeddedClip", sampleCount / channels, channels, sampleRate, false);
            clip.SetData(audioData, 0);
            return clip;
        }


        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromResource(string resourceName)
        {
            AudioClip sound = null;

            if (!audioPool.ContainsKey(resourceName))
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Debug.LogError($"Failed to load resource stream for '{resourceName}'.");
                        return null;
                    }

                    byte[] wavData = new byte[stream.Length];
                    stream.Read(wavData, 0, wavData.Length);
                    sound = LoadWav(wavData);
                    audioPool.Add(resourceName, sound);
                }
            }
            else
            {
                sound = audioPool[resourceName];
            }

            return sound;
        }
        public static AudioClip LoadSoundFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"Audio file not found at path: {path}");
                return null;
            }

            if (!audioPool.ContainsKey(path))
            {
                byte[] wavData = File.ReadAllBytes(path);
                AudioClip clip = LoadWav(wavData);
                if (clip != null)
                {
                    audioPool.Add(path, clip);
                }
                return clip;
            }
            else
            {
                return audioPool[path];
            }
        }
        private static GameObject audiomgr = null;
        public static void Play2DAudio(AudioClip sound, float volume)
        {
            if (audiomgr == null)
            {
                audiomgr = new GameObject("2DAudioMgr");
                AudioSource temp = audiomgr.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }
            AudioSource ausrc = audiomgr.GetComponent<AudioSource>();
            ausrc.volume = (bgm != null && bgm.GetComponent<AudioSource>().isPlaying) ? volume / 2f : volume;
            ausrc.PlayOneShot(sound);
        }

        private static GameObject bgm = null;
        public static void PlaySoundtrack()
        {
            if (bgm == null)
            {
                bgm = new GameObject("2DAudioMgrbgm");
                AudioSource temp = bgm.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }
            AudioSource ausrc = bgm.GetComponent<AudioSource>();
            if (!ausrc.isPlaying && bgmEnabled)
            {
                string targetFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\GorillaSource";
                string targetFile = Path.Combine(targetFolder, "bg.wav");
                ausrc.volume = 1f;
                ausrc.loop = true;
                ausrc.clip = LoadSoundFromFile(targetFile);
                ausrc.Play();

                if (networkedBGM && PhotonNetwork.InRoom)
                {
                    GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                    GorillaTagger.Instance.myRecorder.AudioClip = LoadSoundFromFile(targetFile);
                    GorillaTagger.Instance.myRecorder.RestartRecording(true);
                }
            }
        }

        public static void StopSoundtrack()
        {
            if (bgm != null)
            {
                AudioSource ausrc = bgm.GetComponent<AudioSource>();
                if (ausrc.isPlaying)
                {
                    ausrc.Stop();

                    if (networkedBGM && PhotonNetwork.InRoom)
                    {
                        GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                        GorillaTagger.Instance.myRecorder.AudioClip = null;
                        GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    }
                } 
            }
        }

        private static GameObject walk = null;
        public static void PlayWalk()
        {
            if (walk == null)
            {
                walk = new GameObject("2DAudioMgrwalk");
                AudioSource temp = walk.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }
            AudioSource ausrc = walk.GetComponent<AudioSource>();
            if (!ausrc.isPlaying)
            {
                ausrc.volume = 1f;
                ausrc.loop = true;
                ausrc.clip = LoadSoundFromResource("GorillaSource.Resources.walk.wav");
                ausrc.Play();
            }
        }

        public static void StopWalk()
        {
            if (walk != null)
            {
                AudioSource ausrc = walk.GetComponent<AudioSource>();
                if (ausrc.isPlaying)
                    ausrc.Pause();
            }
        }

        public static Texture2D LoadTextureFromResource(string resourcePath)
        {
            Texture2D texture = new Texture2D(2, 2);

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
            if (stream != null)
            {
                byte[] fileData = new byte[stream.Length];
                stream.Read(fileData, 0, (int)stream.Length);
                texture.LoadImage(fileData);
            }
            else
            {
                Debug.LogError("Failed to load texture from resource: " + resourcePath);
            }
            return texture;
        }
    }
}