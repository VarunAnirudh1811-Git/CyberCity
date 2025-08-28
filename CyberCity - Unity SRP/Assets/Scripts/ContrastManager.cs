/*
 * using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering; // for GraphicsFormat (if needed)

[ExecuteAlways]
public class ContrastManager : MonoBehaviour
{
    public static ContrastManager Instance { get; private set; }

    [Header("Camera & Layers")]
    public Camera targetCamera;
    public LayerMask contrastObjectsLayer;   // put tracked objects on this layer

    [Header("Render Targets")]
    [Range(64, 1024)] public int sampleResolution = 256;
    public RenderTextureFormat colorFormat = RenderTextureFormat.ARGB32;

    [Header("Shaders")]
    public Shader objectReplacementShader;   // set to ObjectReplacement.shader (below)
    public ComputeShader contrastCompute;    // set to ContrastCompute.compute (below)

    [Header("Objects")]
    [Tooltip("Upper bound of tracked objects (IDs 1..max-1).")]
    public int maxObjects = 64;

    // Runtime: registered objects and IDs
    private readonly List<SalientObject> _objects = new();
    private readonly Dictionary<int, SalientObject> _idToObject = new();
    private readonly Dictionary<SalientObject, int> _objToId = new();
    private int _nextId = 1;

    // RTs
    private RenderTexture _rtBackground;
    private RenderTexture _rtObject;

    // GPU buffer: uint4 per object: [sumObjLum, sumBgLum, sumDeltaE, count]
    private ComputeBuffer _resultBuffer;
    private const int STRIDE_UINT4 = sizeof(uint) * 4;

    private bool _pendingRecompute = true;

    void Awake()
    {
        if (Instance && Instance != this) { DestroyImmediate(this); return; }
        Instance = this;
    }

    void OnEnable()
    {
        if (!targetCamera) targetCamera = Camera.main;
        AllocateRTs();
        AllocateBuffers();
    }

    void OnDisable()
    {
        ReleaseRTs();
        ReleaseBuffers();
    }

    void Update()
    {
        if (_pendingRecompute) StartCoroutine(RunContrastPipeline());
        _pendingRecompute = false;
    }

    public void RequestRecompute() => _pendingRecompute = true;

    public void RegisterObject(SalientObject so)
    {
        if (_objToId.ContainsKey(so)) return;
        _objects.Add(so);
        int id = _nextId++;
        if (id >= maxObjects) Debug.LogWarning("Hit maxObjects; increase pool.");
        _objToId[so] = id;
        _idToObject[id] = so;

        // Push ID into all renderers on this object (so replacement shader can read it)
        var rens = so.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rens)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_ContrastObjectID", id / 255f); // encode as 0..1
            r.SetPropertyBlock(mpb);
        }

        RequestRecompute();
    }

    public void UnregisterObject(SalientObject so)
    {
        if (!_objToId.TryGetValue(so, out int id)) return;
        _objToId.Remove(so);
        _idToObject.Remove(id);
        _objects.Remove(so);
        RequestRecompute();
    }

    void AllocateRTs()
    {
        ReleaseRTs();

        _rtBackground = new RenderTexture(sampleResolution, sampleResolution, 24, colorFormat)
        {
            name = "RT_Background",
            useMipMap = false,
            autoGenerateMips = false,
            enableRandomWrite = false
        };
        _rtBackground.Create();

        _rtObject = new RenderTexture(sampleResolution, sampleResolution, 24, colorFormat)
        {
            name = "RT_Object",
            useMipMap = false,
            autoGenerateMips = false,
            enableRandomWrite = false
        };
        _rtObject.Create();
    }

    void ReleaseRTs()
    {
        if (_rtBackground) { _rtBackground.Release(); DestroyImmediate(_rtBackground); _rtBackground = null; }
        if (_rtObject) { _rtObject.Release(); DestroyImmediate(_rtObject); _rtObject = null; }
    }

    void AllocateBuffers()
    {
        ReleaseBuffers();
        _resultBuffer = new ComputeBuffer(maxObjects, STRIDE_UINT4, ComputeBufferType.Default);
        ZeroResults();
    }

    void ReleaseBuffers()
    {
        if (_resultBuffer != null) { _resultBuffer.Release(); _resultBuffer = null; }
    }

    void ZeroResults()
    {
        // Fill with zeros
        uint[] zeros = new uint[maxObjects * 4];
        _resultBuffer.SetData(zeros);
    }

    IEnumerator RunContrastPipeline()
    {
        if (!targetCamera || !objectReplacementShader || !contrastCompute)
        {
            Debug.LogWarning("ContrastManager: Missing references.");
            yield break;
        }

        // --- 1) BACKGROUND PASS: render scene WITHOUT contrast objects ---
        int originalMask = targetCamera.cullingMask;

        targetCamera.cullingMask = originalMask & ~contrastObjectsLayer.value;
        targetCamera.targetTexture = _rtBackground;
        targetCamera.Render();
        targetCamera.targetTexture = null;

        yield return new WaitForEndOfFrame(); // ensure RT is ready

        // --- 2) OBJECT PASS: render ONLY contrast objects with replacement shader (writes ID in A) ---
        targetCamera.cullingMask = contrastObjectsLayer.value;
        targetCamera.targetTexture = _rtObject;
        targetCamera.RenderWithShader(objectReplacementShader, "RenderType");
        targetCamera.targetTexture = null;

        // Restore normal culling
        targetCamera.cullingMask = originalMask;

        // --- 3) COMPUTE PASS: compare per-pixel object vs background; accumulate per-object ---
        ZeroResults();

        int kernel = contrastCompute.FindKernel("CSMain");
        contrastCompute.SetTexture(kernel, "_ObjectTex", _rtObject);
        contrastCompute.SetTexture(kernel, "_BackgroundTex", _rtBackground);
        contrastCompute.SetInt("_TexWidth", _rtObject.width);
        contrastCompute.SetInt("_TexHeight", _rtObject.height);
        contrastCompute.SetInt("_MaxObjects", maxObjects);
        contrastCompute.SetBuffer(kernel, "_Results", _resultBuffer);

        int gx = Mathf.CeilToInt(_rtObject.width / 8f);
        int gy = Mathf.CeilToInt(_rtObject.height / 8f);
        contrastCompute.Dispatch(kernel, gx, gy, 1);

        // --- 4) READBACK + DISTRIBUTE ---
        bool done = false;
        AsyncGPUReadback.Request(_resultBuffer, req =>
        {
            if (req.hasError) { Debug.LogError("GPU readback error"); done = true; return; }
            var data = req.GetData<uint>();
            // Decode: uint4 per object  [sumObjLum, sumBgLum, sumDeltaE, count]
            const float SCALE = 1048576f; // must match compute shader
            for (int id = 1; id < maxObjects; id++)
            {
                int idx = id * 4;
                uint sumObjU = data[idx + 0];
                uint sumBgU = data[idx + 1];
                uint sumDEU = data[idx + 2];
                uint cntU = data[idx + 3];

                if (cntU == 0) continue;

                float count = cntU;
                float avgObjLum = (sumObjU / SCALE) / count; // linear luminance
                float avgBgLum = (sumBgU / SCALE) / count;
                float avgDE76 = (sumDEU / SCALE) / count; // 0..~100 typically

                // WCAG contrast ratio using relative luminance
                float Lmax = Mathf.Max(avgObjLum, avgBgLum);
                float Lmin = Mathf.Min(avgObjLum, avgBgLum);
                float wcag = (Lmax + 0.05f) / (Lmin + 0.05f);

                // Normalize ΔE to ~0..1 by dividing by 100 (ΔE>100 is rare)
                float color01 = Mathf.Clamp01(avgDE76 / 100f);

                if (_idToObject.TryGetValue(id, out var so))
                    so.SetContrastValues(color01, wcag);
            }
            done = true;
        });

        // tiny spin until readback callback (single frame)
        while (!done) yield return null;
    }
}
*/