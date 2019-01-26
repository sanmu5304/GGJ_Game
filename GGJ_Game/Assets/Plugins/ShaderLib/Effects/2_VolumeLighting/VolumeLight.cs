using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class VolumeLight : MonoBehaviour
{
    [Header("VolumeLight Settings")]
    [Space]
    public float strength = 1.0f;
    public float fallback = 1.0f;

    [Range(0.0f, 1.0f)]
    public float ScatteringCoef = 1f;
    [Range(0.0f, 0.1f)]
    public float ExtinctionCoef = 0.01f;
    [Range(0.0f, 1.0f)]
    public float SkyboxExtinctionCoef = 0.9f;
    [Range(0.0f, 0.999f)]
    public float MieG = 0.07f;
    public float depthBias = 0.0f;
    public int sampleCount = 4;
    [Header("增强明暗對比")]
    public bool contrast;
    // Use this for initialization
    [Header("Noise Settings")]
    [Space]
    public bool isNoise;
    public Texture noiseTex;
    public Vector2 NoiseScale = new Vector2(1.0f, 1.0f);
    public float NoiseIntensity = 1.0f;
    public float NoiseIntensityOffset = 0.3f;
    public Vector2 NoiseVelocity = new Vector2(0.1f, 0.1f);

    [Header("是否使用自定义深度图")]
    public bool useCustomLightDepthMap = true;

    private Light _light;
    private Material _material;
    private CommandBuffer _shadowCommandBuffer;
    public Shader volumeLightShader;
    private bool isInit;
    //CustomDepthMap相关
    private bool oldCustomDepthFlag;
    private RenderTexture customLightDepthTex;
    private Camera lightCam;
    [Header("\"VolumeLight\" = \"DepthMask\"")]
    public Shader depthShader;
    public Material VolumetricMaterial { get { return _material; } }

    //public bool HeightFog = false;
    //[Range(0, 0.5f)]
    //public float HeightScale = 0.10f;
    //public float GroundLevel = 0;
    //public bool Noise = false;

    private void Awake()
    {
    }

    void Start()
    {
        oldCustomDepthFlag = useCustomLightDepthMap;
    }

    private void Init()
    {
        //每个Light的深度图
        //RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
        volumeLightShader = Shader.Find("ChillyRoom/VolumeLight/RenderLight");
        depthShader = Shader.Find("ChillyRoom/VolumeLight/DepthFromLight");
        if (_material == null)
        {
            _material = new Material(volumeLightShader);
            _material.hideFlags = HideFlags.HideAndDontSave;
        }
        //m_ShadowmapCopy = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        //m_ShadowmapCopy.DiscardContents();
        if (_shadowCommandBuffer == null)
        {
            _shadowCommandBuffer = new CommandBuffer();
            _shadowCommandBuffer.name = "Spot Light Depth Command Buffer";
        }
        //_commandBuffer.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);
        //_commandBuffer.SetGlobalTexture("_LightDepthTexture", m_ShadowmapCopy);


        //非 directional light
        _light = this.transform.GetComponent<Light>();
        _light.type = LightType.Spot;
        if (_light == null || _light.gameObject == null || _light.enabled == false)
        {
            VolumeLightMgr.PreRenderEvent -= VolumeLightMgr_PreRenderEvent;
        }
        else
        {
            _light.RemoveAllCommandBuffers();
            if (useCustomLightDepthMap)
            {
                InitCustomDepthCamera();
                _light.shadows = LightShadows.None;
                if (VolumeLightMgr.Instance.enabled != false)
                    VolumeLightMgr.Instance._camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, _shadowCommandBuffer);
            }
            else
            {
                _light.shadows = LightShadows.Soft;
                _light.AddCommandBuffer(LightEvent.AfterShadowMap, _shadowCommandBuffer);
            }
        }
        isInit = true;
    }

    void InitCustomDepthCamera()
    {
        if (customLightDepthTex == null)
            customLightDepthTex = RenderTexture.GetTemporary(1024, 1024);
        lightCam = this.GetComponent<Camera>();
        if (lightCam == null)
        {
            lightCam = this.gameObject.AddComponent<Camera>();
            lightCam.fieldOfView = _light.spotAngle;
            lightCam.backgroundColor = Color.black;
            lightCam.targetTexture = customLightDepthTex;
            lightCam.clearFlags = CameraClearFlags.SolidColor;
            lightCam.depth = -10;
            lightCam.allowHDR = false;
            lightCam.allowMSAA = false;
            lightCam.useOcclusionCulling = false;
            lightCam.farClipPlane = 200;
            lightCam.nearClipPlane = 1.0f;
            lightCam.SetReplacementShader(depthShader, "RenderType");
        }
        else
        {
            lightCam.enabled = true;
            lightCam.targetTexture = customLightDepthTex;
            lightCam.SetReplacementShader(depthShader, "RenderType");
        }
    }

    void OnEnable()
    {
        Init();
        //将VolumeLight添加到VolumeLightMgr
        VolumeLightMgr.Instance.AddVolumeLight(this, VolumeLightMgr_PreRenderEvent);
    }

    void OnDisable()
    {
        isInit = false;
        removeCommandBuffer();

        VolumeLightMgr.Instance.RemoveVolumeLight(this, VolumeLightMgr_PreRenderEvent);
    }

    void OnDestroy()
    {
        if(!VolumeLightMgr.isDestroy)
            VolumeLightMgr.Instance.DestroyVolumeLight(this);
    }

    public void removeCommandBuffer()
    {
        if (_shadowCommandBuffer != null)
        {
            if (useCustomLightDepthMap)
            {
                VolumeLightMgr.Instance._camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, _shadowCommandBuffer);
            }
            else
            {
                _light.RemoveCommandBuffer(LightEvent.AfterShadowMap, _shadowCommandBuffer);
            }
        }
    }

    private void VolumeLightMgr_PreRenderEvent(VolumeLightMgr renderer, Matrix4x4 viewProj)
    {
        if (_light == null || _shadowCommandBuffer == null)
        {
            return;
        }
        _material.SetVector("_CameraForward", VolumeLightMgr.Instance._camera.transform.forward);

        _material.SetInt("_SampleCount", sampleCount);
        _material.SetFloat("_Bias", depthBias);
        _material.SetVector("_MieG", new Vector4(1 - (MieG * MieG), 1 + (MieG * MieG), 2 * MieG, 1.0f / (4.0f * Mathf.PI)));
        _material.SetVector("_VolumetricLight", new Vector4(ScatteringCoef, ExtinctionCoef, _light.range, 1.0f - SkyboxExtinctionCoef));

        //_material.SetVector("_LightPos", new Vector4(this.transform.position.x, this.transform.position.y, this.transform.position.z, 1));
        _material.SetTexture("_CameraDepthTexture", renderer.GetVolumeLightDepthBuffer());

        _material.SetVector("_halfResolution", new Vector2(renderer.GetVolumeLightBuffer().width, renderer.GetVolumeLightBuffer().height) / 2);
        _material.SetFloat("_Strength", strength);
        _material.SetFloat("_Fallback", fallback);
        SetupSpotLight(renderer, viewProj);
    }

    //public Mesh SpotLightMesh;
    private void SetupSpotLight(VolumeLightMgr renderer, Matrix4x4 viewProj)
    {
        _shadowCommandBuffer.Clear();
        //如果CustomLightDepthTex不为空，就使用指定的DepthTex，否则使用unity灯光自带的
        if (useCustomLightDepthMap)
        {
            _material.SetTexture("_LightDepthTexture", customLightDepthTex);
        }
        else
        {
            //拷贝depth map
            RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
            //设置当前深度图的采样模式
            _shadowCommandBuffer.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);
            _shadowCommandBuffer.SetGlobalTexture("_LightDepthTexture", shadowmap);
        }
        //------------------------------------------------------
        int pass = GetCameraInSpotLightBounds(VolumeLightMgr.Instance._camera);
        Mesh mesh = VolumeLightMgr.GetSpotLightMesh();
        float scale = _light.range;
        float angleScale = Mathf.Tan((_light.spotAngle + 1) * 0.5f * Mathf.Deg2Rad) * _light.range;

        Matrix4x4 world = Matrix4x4.TRS(_light.transform.position, _light.transform.rotation, new Vector3(angleScale, angleScale, scale));
        Matrix4x4 view = Matrix4x4.TRS(_light.transform.position, _light.transform.rotation, Vector3.one).inverse;

        //NDC
        Matrix4x4 clip = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity, new Vector3(-0.5f, -0.5f, 1.0f));

        Matrix4x4 proj = Matrix4x4.Perspective(_light.spotAngle, 1, 0, 1);
        _material.SetMatrix("_MyLightMatrix0", clip * proj * view);
        _material.SetMatrix("_WorldViewProj", viewProj * world);

        _material.SetVector("_LightPos", new Vector4(_light.transform.position.x, _light.transform.position.y, _light.transform.position.z, 1.0f / (_light.range * _light.range)));
        _material.SetVector("_LightColor", _light.color * _light.intensity);

        Vector3 apex = _light.transform.position;
        Vector3 axis = _light.transform.forward;

        // plane equation ax + by + cz + d = 0; precompute d here to lighten the shader
        Vector3 center = apex + axis * _light.range;
        float d = -Vector3.Dot(center, axis);

        _material.SetFloat("_PlaneD", d);
        _material.SetFloat("_CosAngle", Mathf.Cos((_light.spotAngle + 1) * 0.5f * Mathf.Deg2Rad));

        _material.SetVector("_ConeApex", new Vector4(apex.x, apex.y, apex.z));
        _material.SetVector("_ConeAxis", new Vector4(axis.x, axis.y, axis.z));

        _material.EnableKeyword("SPOT");
        _material.DisableKeyword("NOISE");
        if (_light.cookie == null)
        {
            _material.SetTexture("_LightCookieTex", VolumeLightMgr.GetDefaultSpotCookie());
        }
        else
        {
            _material.SetTexture("_LightCookieTex", _light.cookie);
        }


        clip = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f));

        if (VolumeLightMgr._reversedZ)
        {
            proj = Matrix4x4.Perspective(_light.spotAngle, 1, _light.range, _light.shadowNearPlane);
            _material.DisableKeyword("XIAO_MI");
        }
        else
        {
            proj = Matrix4x4.Perspective(_light.spotAngle, 1, _light.shadowNearPlane, _light.range);
            if (Application.platform == RuntimePlatform.Android)
                _material.EnableKeyword("XIAO_MI");
        }
        Matrix4x4 m = clip * proj;
        m[0, 2] *= -1;
        m[1, 2] *= -1;
        m[2, 2] *= -1;
        m[3, 2] *= -1;
        //view = _light.transform.worldToLocalMatrix;
        _material.SetMatrix("_MyWorld2Shadow", m * view);

        _material.EnableKeyword("SHADOWS_DEPTH");
        if (isNoise)
        {
            _material.SetTexture("_NoiseTex", noiseTex);
            _material.SetVector("_NoiseVelocity", new Vector4(NoiseVelocity.x * 1.0f / NoiseScale.x, NoiseVelocity.y * 1.0f / NoiseScale.y));
            _material.SetVector("_NoiseData", new Vector4(1.0f / NoiseScale.x, 1.0f / NoiseScale.y, NoiseIntensity, NoiseIntensityOffset));
            _material.EnableKeyword("NOISE");
        }
        else
            _material.DisableKeyword("NOISE");

        if (contrast)
        {
            //_material.SetFloat("_Contrast", contrast);
            _material.EnableKeyword("CONTRAST");
        }
        else
            _material.DisableKeyword("CONTRAST");

        _shadowCommandBuffer.SetRenderTarget(renderer.GetVolumeLightBuffer());
        //_shadowCommandBuffer.ClearRenderTarget(false, true, Color.white);
        _shadowCommandBuffer.DrawMesh(mesh, world, _material, 0, pass);


        //if (CustomRenderEvent != null)
        //    CustomRenderEvent(renderer, this, _commandBuffer, viewProj);

    }

    // Update is called once per frame
    void Update()
    {
        if (!isInit)
            return;
        if (useCustomLightDepthMap)
        {
            lightCam.fieldOfView = _light.spotAngle;
            lightCam.farClipPlane = _light.range;
        }
        else
        {
            if (lightCam != null && lightCam.enabled)
                lightCam.enabled = false;
        }
        if (useCustomLightDepthMap != oldCustomDepthFlag)
        {
            DestroyImmediate(_material);
            _material = new Material(volumeLightShader);
            _material.hideFlags = HideFlags.HideAndDontSave;
            if (useCustomLightDepthMap)
            {
                _light.shadows = LightShadows.None;
                _light.RemoveAllCommandBuffers();
                _shadowCommandBuffer.Clear();
                VolumeLightMgr.Instance._camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, _shadowCommandBuffer);
            }
            else
            {
                _light.shadows = LightShadows.Soft;
                VolumeLightMgr.Instance._camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, _shadowCommandBuffer);
                _shadowCommandBuffer.Clear();
                _light.AddCommandBuffer(LightEvent.AfterShadowMap, _shadowCommandBuffer);
            }
        }
        oldCustomDepthFlag = useCustomLightDepthMap;
    }

    private int GetCameraInSpotLightBounds(Camera activeCam)
    {
        // check range
        float distance = Vector3.Dot(_light.transform.forward, (activeCam.transform.position - _light.transform.position));
        float extendedRange = _light.range + 1;
        if (distance > (extendedRange))
            return 0;

        // check angle
        float cosAngle = Vector3.Dot(_light.transform.forward, (activeCam.transform.position - _light.transform.position).normalized);
        if ((Mathf.Acos(cosAngle) * Mathf.Rad2Deg) > (_light.spotAngle + 3) * 0.5f)
            return 0;

        return 1;
    }
}