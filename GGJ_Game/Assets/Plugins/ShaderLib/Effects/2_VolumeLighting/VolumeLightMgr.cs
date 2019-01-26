using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[ExecuteInEditMode]
public class VolumeLightMgr : MonoBehaviour
{
    private static VolumeLightMgr instance;
    public static VolumeLightMgr Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Camera.main.transform.GetComponent<VolumeLightMgr>();
                if (instance == null)
                {
                    instance = Camera.main.gameObject.AddComponent<VolumeLightMgr>();
                }   
            }
            if (instance == null)
            {
                Debug.LogError("VolumeLightMgr不存在");
            }
            return instance;
        }
    }
    private static Mesh _spotLightMesh;
    private Material _blitAddMaterial;
    private Material _bilateralBlurMaterial;    //down sample depth map
    private Material _DownSampleMaterial;

    private CommandBuffer _preLightPass;
    public CommandBuffer GlobalCommandBuffer { get { return _preLightPass; } }

    private static Texture _defaultSpotCookie;
    public Texture DefaultSpotCookie;
    public static int sampleCount = 4;
    private Camera _cam;
    public Camera _camera
    {
        get
        {
            if (_cam == null)
                _cam = GetComponent<Camera>();
            return _cam;
        }
    }
    //Light Manager
    public List<VolumeLight> volumelights = new List<VolumeLight>();
    public List<Action<VolumeLightMgr, Matrix4x4>> volumelightEvts = new List<Action<VolumeLightMgr, Matrix4x4>>();

    public void AddVolumeLight(VolumeLight light, Action<VolumeLightMgr, Matrix4x4> VolumeLightMgr_PreRenderEvent)
    {
        if (volumelightEvts.Contains(VolumeLightMgr_PreRenderEvent))
        {
            volumelightEvts.Remove(VolumeLightMgr_PreRenderEvent);
        }

        if (!volumelights.Contains(light))
            volumelights.Add(light);
        volumelightEvts.Add(VolumeLightMgr_PreRenderEvent);
        VolumeLightMgr.PreRenderEvent += VolumeLightMgr_PreRenderEvent;
    }

    public void RemoveVolumeLight(VolumeLight light, Action<VolumeLightMgr, Matrix4x4> VolumeLightMgr_PreRenderEvent)
    {
        if (!volumelights.Contains(light))
            return;
        VolumeLightMgr.PreRenderEvent -= VolumeLightMgr_PreRenderEvent;
    }

    public void DestroyVolumeLight(VolumeLight light)
    {
        if (volumelights.Contains(light))
            volumelights.Remove(light);
    }

    public static Texture GetDefaultSpotCookie()
    {
        return _defaultSpotCookie;
    }

    public static Mesh GetSpotLightMesh()
    {
        return _spotLightMesh;
    }

    //public Light spotLiight;


    //public Material ditherMat;
    public Texture2D _ditheringTexture;

    public enum VolumtericResolution
    {
        Full,
        Half,
        Quarter
    };
    public VolumtericResolution Resolution;
    private VolumtericResolution curResolution;
    private RenderTexture _volumeLightTexture;
    private RenderTexture _halfVolumeLightTexture;
    private RenderTexture _halfDepthTexture;
    private RenderTexture _quaterVolumeLightTexture;
    private RenderTexture _quaterDepthTexture;

    public static string msg = "";
    public static bool isDestroy = false;
    void OnGUI()
    {
        //    GUILayout.Space(50);
        //    GUILayout.Label(msg);
        //    GUILayout.Label("smapleCount: " + sampleCount);
        //    //GUILayout.Label("spotAngle: " + spotLiight.spotAngle);
        //    GUILayout.Label("bias: " + bias);
        //    GUILayout.Label("_reversedZ: " + _reversedZ);
        //    GUI.skin.horizontalSlider.fixedHeight = 50;
        //    GUI.skin.horizontalSlider.fixedWidth = 50 * 16;
        //    GUI.skin.horizontalSliderThumb.fixedHeight = 50;
        //    GUI.skin.horizontalSliderThumb.fixedWidth = 50;
        //    sampleCount = (int)GUILayout.HorizontalSlider(sampleCount, 1, 64);
        //    //spotLiight.spotAngle = GUILayout.HorizontalSlider(spotLiight.spotAngle, 15, 150);
        //    Resolution = (VolumtericResolution)GUILayout.SelectionGrid((int)Resolution, new string[] { "Full", "Half", "Quater" }, 3);
        //    bias = GUILayout.HorizontalSlider(bias, 2, 5);
    }

    public static bool _reversedZ = false;

    void Init()
    {
#if UNITY_5_5_OR_NEWER
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal || SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStation4 ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan || SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOne)
        {
            _reversedZ = true;
        }
#endif
        if (QualitySettings.shadows == ShadowQuality.Disable)
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 50;
        }
        _cam = GetComponent<Camera>();
        Shader shader = Shader.Find("ChillyRoom/VolumeLight/BlitAdd");
        _blitAddMaterial = new Material(shader);
        _blitAddMaterial.hideFlags = HideFlags.HideAndDontSave;

        shader = Shader.Find("ChillyRoom/VolumeLight/BilateralBlur");
        _bilateralBlurMaterial = new Material(shader);
        _bilateralBlurMaterial.hideFlags = HideFlags.HideAndDontSave;

        shader = Shader.Find("ChillyRoom/VolumeLight/DownSampleDepth");
        _DownSampleMaterial = new Material(shader);
        _DownSampleMaterial.hideFlags = HideFlags.HideAndDontSave;

        //获取深度图
        _preLightPass = new CommandBuffer();
        _preLightPass.name = "PreLight";

        _camera.depthTextureMode = DepthTextureMode.Depth;
        _camera.AddCommandBuffer(CameraEvent.AfterDepthTexture, _preLightPass);

        ChangeResolution();


        if (_spotLightMesh == null)
        {
            _spotLightMesh = CreateSpotLightMesh();
        }

        if (_defaultSpotCookie == null)
        {
            _defaultSpotCookie = DefaultSpotCookie;
        }

        GenerateDitherTexture();
    }

    void Update()
    {
        if (Resolution != curResolution)
        {
            ChangeResolution();
            curResolution = Resolution;
        }
        _defaultSpotCookie = DefaultSpotCookie;

    }



    private Matrix4x4 _viewProj;

    public static event Action<VolumeLightMgr, Matrix4x4> PreRenderEvent;


    void OnEnable()
    {
        isDestroy = false;
        Init();
        for (int i = 0; i < volumelights.Count; i++)
        {
            if (volumelights[i] != null && volumelights[i].enabled == false)
                volumelights[i].enabled = true;
        }
    }

    void OnDisable()
    {
        _camera.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, _preLightPass);
        for (int i = 0; i < volumelights.Count; i++)
        {
            if (volumelights[i] != null && volumelights[i].enabled == true)
                volumelights[i].enabled = false;
        }
    }

    void OnDestroy()
    {
        isDestroy = true;
        _camera.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, _preLightPass);
        for (int i = 0; i < volumelights.Count; i++)
        {
            if (volumelights[i] != null && volumelights[i].enabled == true)
                volumelights[i].enabled = false;
        }
    }


    void OnPreRender()
    {
        if (_preLightPass == null)
            return;

        _cam = GetComponent<Camera>();
        Matrix4x4 proj = Matrix4x4.Perspective(_camera.fieldOfView, _camera.aspect, 0.01f, _camera.farClipPlane);
        //opengl（-1,1） -> DX（0,1）： x轴不变，Y要翻转（uv坐标），Z要翻转（左右手坐标系），Z要*0.5+0.5：dx NDC坐标不同
        //proj = Matrix4x4.TRS(new Vector3(0f, 0f, 0.5f), Quaternion.identity, new Vector3(1f, -1f, -0.5f)) * proj;
        proj = GL.GetGPUProjectionMatrix(proj, true);

        _viewProj = proj * _camera.worldToCameraMatrix;

        _preLightPass.Clear();
        if (Resolution == VolumtericResolution.Full)
        {
            _preLightPass.SetRenderTarget(_volumeLightTexture);
        }
        else if (Resolution == VolumtericResolution.Half)
        {

            _preLightPass.SetRenderTarget(_halfDepthTexture);
            _preLightPass.ClearRenderTarget(true, true, Color.black);

            // down sample depth
            _preLightPass.Blit(null, _halfDepthTexture, _DownSampleMaterial, 0);

            _preLightPass.SetRenderTarget(_halfVolumeLightTexture);
        }
        else if (Resolution == VolumtericResolution.Quarter)
        {
            _preLightPass.SetRenderTarget(_halfDepthTexture);
            _preLightPass.ClearRenderTarget(true, true, Color.black);

            // down sample depth to half res
            _preLightPass.Blit(null, _halfDepthTexture, _DownSampleMaterial, 0);

            _preLightPass.SetRenderTarget(_quaterDepthTexture);
            _preLightPass.ClearRenderTarget(true, true, Color.black);

            // down sample depth to quarter res
            _preLightPass.Blit(null, _quaterDepthTexture, _DownSampleMaterial, 1);

            _preLightPass.SetRenderTarget(_quaterVolumeLightTexture);
        }
        //clear 无效的color
        _preLightPass.ClearRenderTarget(true, true, Color.black);

        UpdateMaterialParameters();
        //委托
        if (PreRenderEvent != null)
        {
            PreRenderEvent(this, _viewProj);
        }
    }
    [ImageEffectOpaque] //在透明物体前，非透明物体后
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (Resolution == VolumtericResolution.Full)
        {
            //使用GetTempory会自动清空
            RenderTexture temp = RenderTexture.GetTemporary(_volumeLightTexture.width, _volumeLightTexture.height, 0, RenderTextureFormat.ARGB32);
            temp.filterMode = FilterMode.Bilinear;

            //blur一下
            temp.DiscardContents();
            // horizontal bilateral blur at full res
            Graphics.Blit(_volumeLightTexture, temp, _bilateralBlurMaterial, 0);

            //使用前需要清空，不然就调用这个
            _volumeLightTexture.DiscardContents();
            // vertical bilateral blur at full res
            Graphics.Blit(temp, _volumeLightTexture, _bilateralBlurMaterial, 1);
            RenderTexture.ReleaseTemporary(temp);

            //叠加到场景中
            _blitAddMaterial.SetTexture("_Source", source);
            Graphics.Blit(_volumeLightTexture, destination, _blitAddMaterial, 0);

        }
        else if (Resolution == VolumtericResolution.Half)
        {
            RenderTexture temp = RenderTexture.GetTemporary(_halfVolumeLightTexture.width, _halfVolumeLightTexture.height, 0, RenderTextureFormat.ARGB32);
            temp.filterMode = FilterMode.Bilinear;
            temp.DiscardContents();

            // horizontal bilateral blur at half res
            Graphics.Blit(_halfVolumeLightTexture, temp, _bilateralBlurMaterial, 2);

            _halfVolumeLightTexture.DiscardContents();
            // vertical bilateral blur at half res
            Graphics.Blit(temp, _halfVolumeLightTexture, _bilateralBlurMaterial, 3);


            _volumeLightTexture.DiscardContents();
            // upscale to full res
            Graphics.Blit(_halfVolumeLightTexture, _volumeLightTexture, _bilateralBlurMaterial, 4);

            RenderTexture.ReleaseTemporary(temp);

            //叠加到场景中
            _blitAddMaterial.SetTexture("_Source", source);
            Graphics.Blit(_volumeLightTexture, destination, _blitAddMaterial, 0);
        }
        else if (Resolution == VolumtericResolution.Quarter)
        {
            RenderTexture temp = RenderTexture.GetTemporary(_quaterVolumeLightTexture.width, _quaterVolumeLightTexture.height, 0, RenderTextureFormat.ARGB32);
            temp.filterMode = FilterMode.Bilinear;
            temp.DiscardContents();

            // horizontal bilateral blur at half res
            Graphics.Blit(_quaterVolumeLightTexture, temp, _bilateralBlurMaterial, 5);

            _quaterVolumeLightTexture.DiscardContents();
            // vertical bilateral blur at half res
            Graphics.Blit(temp, _quaterVolumeLightTexture, _bilateralBlurMaterial, 6);
            RenderTexture.ReleaseTemporary(temp);

            _halfVolumeLightTexture.DiscardContents();
            // upscale to half res
            Graphics.Blit(_quaterVolumeLightTexture, _halfVolumeLightTexture, _bilateralBlurMaterial, 7);

            //全分辨率的upscale太耗性能了
            //_volumeLightTexture.DiscardContents();
            // upscale to Full res
            //Graphics.Blit(_halfVolumeLightTexture, _volumeLightTexture, _bilateralBlurMaterial, 4);

            //叠加到场景中
            _blitAddMaterial.SetTexture("_Source", source);
            Graphics.Blit(_halfVolumeLightTexture, destination, _blitAddMaterial, 0);
        }
    }

    //private RenderTexture _downSmapleVolumeLightTexture;
    //private RenderTexture _downSmapleDepthTexture;
    private void ChangeResolution()
    {
        int width = _camera.pixelWidth;
        int height = _camera.pixelHeight;
        if (_volumeLightTexture != null)
            RenderTexture.ReleaseTemporary(_volumeLightTexture);
        if (_halfVolumeLightTexture != null)
            RenderTexture.ReleaseTemporary(_halfVolumeLightTexture);
        if (_halfDepthTexture != null)
            RenderTexture.ReleaseTemporary(_halfDepthTexture);
        if (_quaterVolumeLightTexture != null)
            RenderTexture.ReleaseTemporary(_quaterVolumeLightTexture);
        if (_quaterDepthTexture != null)
            RenderTexture.ReleaseTemporary(_quaterDepthTexture);

        _volumeLightTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        _volumeLightTexture.name = "VolumeLightBuffer";
        _volumeLightTexture.filterMode = FilterMode.Bilinear;
        _volumeLightTexture.hideFlags = HideFlags.HideAndDontSave;
        if (Resolution == VolumtericResolution.Half || Resolution == VolumtericResolution.Quarter)
        {
            _halfVolumeLightTexture = RenderTexture.GetTemporary(width / 2, height / 2, 0, RenderTextureFormat.ARGB32);
            _halfVolumeLightTexture.name = "_halfVolumeLightTexture";
            _halfVolumeLightTexture.filterMode = FilterMode.Bilinear;

            _halfDepthTexture = RenderTexture.GetTemporary(width / 2, height / 2, 0, RenderTextureFormat.ARGB32);
            _halfDepthTexture.name = "_halfDepthTexture";
            _halfDepthTexture.filterMode = FilterMode.Bilinear;
            //_halfDepthTexture.hideFlags = HideFlags.HideAndDontSave;
        }
        if (Resolution == VolumtericResolution.Quarter)
        {
            _quaterVolumeLightTexture = RenderTexture.GetTemporary(width / 4, height / 4, 0, RenderTextureFormat.ARGB32);
            _quaterVolumeLightTexture.name = "_quaterVolumeLightTexture";
            _quaterVolumeLightTexture.filterMode = FilterMode.Bilinear;

            _quaterDepthTexture = RenderTexture.GetTemporary(width / 4, height / 4, 0, RenderTextureFormat.ARGB32);
            _quaterDepthTexture.name = "_quaterDepthTexture";
            _quaterDepthTexture.filterMode = FilterMode.Bilinear;
            //_quaterDepthTexture.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    public RenderTexture GetVolumeLightDepthBuffer()
    {
        //return null;
        if (Resolution == VolumtericResolution.Half)
            return _halfDepthTexture;
        if (Resolution == VolumtericResolution.Quarter)
            return _quaterDepthTexture;
        return null;
    }

    public RenderTexture GetVolumeLightBuffer()
    {
        if (Resolution == VolumtericResolution.Half)
            return _halfVolumeLightTexture;
        else if (Resolution == VolumtericResolution.Quarter)
            return _quaterVolumeLightTexture;
        return _volumeLightTexture;
    }

    private void UpdateMaterialParameters()
    {
        _bilateralBlurMaterial.SetTexture("_HalfDepthTexture", _halfDepthTexture);
        _bilateralBlurMaterial.SetTexture("_HalfMainTexture", _halfVolumeLightTexture);
        _bilateralBlurMaterial.SetTexture("_QuaterDepthTexture", _quaterDepthTexture);
        _bilateralBlurMaterial.SetTexture("_QuaterMainTexture", _quaterVolumeLightTexture);
        _bilateralBlurMaterial.SetTexture("_MainTexture", _volumeLightTexture);
        _DownSampleMaterial.SetTexture("_HalfResDepthBuffer", _halfDepthTexture);
        Shader.SetGlobalTexture("_DitherTexture", _ditheringTexture);
        //ditherMat.mainTexture = _ditheringTexture;
    }

    private Mesh CreateSpotLightMesh()
    {
        // copy & pasted from other project, the geometry is too complex, should be simplified
        Mesh mesh = new Mesh();

        const int segmentCount = 16;
        Vector3[] vertices = new Vector3[2 + segmentCount * 3];
        Color32[] colors = new Color32[2 + segmentCount * 3];

        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(0, 0, 1);

        float angle = 0;
        float step = Mathf.PI * 2.0f / segmentCount;
        float ratio = 0.9f;

        for (int i = 0; i < segmentCount; ++i)
        {
            vertices[i + 2] = new Vector3(-Mathf.Cos(angle) * ratio, Mathf.Sin(angle) * ratio, ratio);
            colors[i + 2] = new Color32(255, 255, 255, 255);
            vertices[i + 2 + segmentCount] = new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 1);
            colors[i + 2 + segmentCount] = new Color32(255, 255, 255, 0);
            vertices[i + 2 + segmentCount * 2] = new Vector3(-Mathf.Cos(angle) * ratio, Mathf.Sin(angle) * ratio, 1);
            colors[i + 2 + segmentCount * 2] = new Color32(255, 255, 255, 255);
            angle += step;
        }

        mesh.vertices = vertices;
        mesh.colors32 = colors;

        int[] indices = new int[segmentCount * 3 * 2 + segmentCount * 6 * 2];
        int index = 0;

        for (int i = 2; i < segmentCount + 1; ++i)
        {
            indices[index++] = 0;
            indices[index++] = i;
            indices[index++] = i + 1;
        }

        indices[index++] = 0;
        indices[index++] = segmentCount + 1;
        indices[index++] = 2;

        for (int i = 2; i < segmentCount + 1; ++i)
        {
            indices[index++] = i;
            indices[index++] = i + segmentCount;
            indices[index++] = i + 1;

            indices[index++] = i + 1;
            indices[index++] = i + segmentCount;
            indices[index++] = i + segmentCount + 1;
        }

        indices[index++] = 2;
        indices[index++] = 1 + segmentCount;
        indices[index++] = 2 + segmentCount;

        indices[index++] = 2 + segmentCount;
        indices[index++] = 1 + segmentCount;
        indices[index++] = 1 + segmentCount + segmentCount;

        //------------
        for (int i = 2 + segmentCount; i < segmentCount + 1 + segmentCount; ++i)
        {
            indices[index++] = i;
            indices[index++] = i + segmentCount;
            indices[index++] = i + 1;

            indices[index++] = i + 1;
            indices[index++] = i + segmentCount;
            indices[index++] = i + segmentCount + 1;
        }

        indices[index++] = 2 + segmentCount;
        indices[index++] = 1 + segmentCount * 2;
        indices[index++] = 2 + segmentCount * 2;

        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = 1 + segmentCount * 2;
        indices[index++] = 1 + segmentCount * 3;

        ////-------------------------------------
        for (int i = 2 + segmentCount * 2; i < segmentCount * 3 + 1; ++i)
        {
            indices[index++] = 1;
            indices[index++] = i + 1;
            indices[index++] = i;
        }

        indices[index++] = 1;
        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = segmentCount * 3 + 1;

        mesh.triangles = indices;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void GenerateDitherTexture()
    {
        if (_ditheringTexture != null)
        {
            return;
        }

        int size = 16;
#if DITHER_4_4
        size = 4;
#endif
        // again, I couldn't make it work with Alpha8
        _ditheringTexture = new Texture2D(size, size, TextureFormat.ARGB32, false, true);
        _ditheringTexture.hideFlags = HideFlags.HideAndDontSave;
        _ditheringTexture.filterMode = FilterMode.Point;
        Color32[] c = new Color32[size * size];

        byte b;
#if DITHER_4_4
        b = (byte)(0.0f / 16.0f * 255); c[0] = new Color32(b, b, b, b);
        b = (byte)(8.0f / 16.0f * 255); c[1] = new Color32(b, b, b, b);
        b = (byte)(2.0f / 16.0f * 255); c[2] = new Color32(b, b, b, b);
        b = (byte)(10.0f / 16.0f * 255); c[3] = new Color32(b, b, b, b);

        b = (byte)(12.0f / 16.0f * 255); c[4] = new Color32(b, b, b, b);
        b = (byte)(4.0f / 16.0f * 255); c[5] = new Color32(b, b, b, b);
        b = (byte)(14.0f / 16.0f * 255); c[6] = new Color32(b, b, b, b);
        b = (byte)(6.0f / 16.0f * 255); c[7] = new Color32(b, b, b, b);

        b = (byte)(3.0f / 16.0f * 255); c[8] = new Color32(b, b, b, b);
        b = (byte)(11.0f / 16.0f * 255); c[9] = new Color32(b, b, b, b);
        b = (byte)(1.0f / 16.0f * 255); c[10] = new Color32(b, b, b, b);
        b = (byte)(9.0f / 16.0f * 255); c[11] = new Color32(b, b, b, b);

        b = (byte)(15.0f / 16.0f * 255); c[12] = new Color32(b, b, b, b);
        b = (byte)(7.0f / 16.0f * 255); c[13] = new Color32(b, b, b, b);
        b = (byte)(13.0f / 16.0f * 255); c[14] = new Color32(b, b, b, b);
        b = (byte)(5.0f / 16.0f * 255); c[15] = new Color32(b, b, b, b);
#endif
#if DITHER_8_8

        int i = 0;
        b = (byte)(1.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(49.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(13.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(61.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(4.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(52.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(16.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(64.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(33.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(17.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(45.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(29.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(36.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(20.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(48.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(32.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(9.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(57.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(5.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(53.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(12.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(60.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(8.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(56.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(41.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(25.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(37.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(21.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(44.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(28.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(40.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(24.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(3.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(51.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(15.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(63.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(2.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(50.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(14.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(62.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(35.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(19.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(47.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(31.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(34.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(18.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(46.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(30.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(11.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(59.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(7.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(55.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(10.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(58.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(6.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(54.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(43.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(27.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(39.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(23.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(42.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(26.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(38.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(22.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
#endif
        int i = 0;
        int cid = 0;
        int[][] pattern = {
            new int[]{ 0, 32,  8, 40,  2, 34, 10, 42},   /* 8x8 Bayer ordered dithering  */
            new int[]{48, 16, 56, 24, 50, 18, 58, 26},   /* pattern.  Each input pixel   */
            new int[]{12, 44,  4, 36, 14, 46,  6, 38},   /* is scaled to the 0..63 range */
            new int[]{60, 28, 52, 20, 62, 30, 54, 22},   /* before looking in this table */
            new int[]{ 3, 35, 11, 43,  1, 33,  9, 41},   /* to determine the action.     */
            new int[]{51, 19, 59, 27, 49, 17, 57, 25},
            new int[]{15, 47,  7, 39, 13, 45,  5, 37},
            new int[]{63, 31, 55, 23, 61, 29, 53, 21} };

        //line0-7
        for (int n = 0; n < 8; n++)
        {
            for (i = 0; i < 8; i++)
            {
                b = (byte)((pattern[n][i] * 4 + 0) + 1 / 255.0 * 255); c[cid++] = new Color32(b, b, b, b);
            }
            for (i = 0; i < 8; i++)
            {
                b = (byte)((pattern[n][i] * 4 + 2) + 1 / 255.0 * 255); c[cid++] = new Color32(b, b, b, b);
            }
        }

        //line8-15
        for (int n = 0; n < 8; n++)
        {
            for (i = 0; i < 8; i++)
            {
                b = (byte)((pattern[n][i] * 4 + 1) + 1 / 255.0 * 255); c[cid++] = new Color32(b, b, b, b);
            }
            for (i = 0; i < 8; i++)
            {
                b = (byte)((pattern[n][i] * 4 + 3) + 1 / 255.0 * 255); c[cid++] = new Color32(b, b, b, b);
            }
        }
        _ditheringTexture.SetPixels32(c);
        _ditheringTexture.Apply();
    }
}