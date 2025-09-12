using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;

public class MLAttentionController : MonoBehaviour
{
    public enum ModelType { None, Classification, Regression }

    [Header("ONNX Models")]
    [SerializeField] private NNModel classificationModelAsset;
    [SerializeField] private NNModel regressionModelAsset;
    [SerializeField] private ModelType activeModel = ModelType.None;

    [Header("NPC References")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private float rotationSpeed = 5f;

    private Model runtimeModel;
    private IWorker worker;
    private List<SalientObject> salientObjects;

    void Awake()
    {
        // Load the correct model at start
        LoadModel(activeModel);

        salientObjects = new List<SalientObject>();
        salientObjects.AddRange(FindObjectsByType<SalientObject>(FindObjectsSortMode.None));
    }

    void Update()
    {
        if (salientObjects == null || salientObjects.Count == 0) return;

        switch (activeModel)
        {
            case ModelType.Classification:
                RunClassification();
                break;
            case ModelType.Regression:
                RunRegression();
                break;
        }
    }

    private void LoadModel(ModelType type)
    {
        worker?.Dispose();
        runtimeModel = null;

        if (type == ModelType.Classification && classificationModelAsset != null)
        {
            runtimeModel = ModelLoader.Load(classificationModelAsset);
        }
        else if (type == ModelType.Regression && regressionModelAsset != null)
        {
            runtimeModel = ModelLoader.Load(regressionModelAsset);
        }

        if (runtimeModel != null)
        {
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
        }
        else
        {
            Debug.LogWarning("No valid model selected or assigned.");
        }
    }

    private void RunClassification()
    {
        SalientObject bestObj = null;
        float bestProb = -1f;

        float[] features = BuildSceneFeatureVector(salientObjects);
        float[] outputs = Evaluate(features);

        for (int i = 0; i < salientObjects.Count && i < outputs.Length; i++)
        {
            float prob = outputs[i];
            if (prob > bestProb)
            {
                bestProb = prob;
                bestObj = salientObjects[i];
            }
        }

        if (bestObj != null)
        {
            Vector3 dir = (bestObj.transform.position - headTransform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            headTransform.rotation = Quaternion.Slerp(headTransform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    private void RunRegression()
    {
        float[] features = BuildSceneFeatureVector(salientObjects);
        float[] outputs = Evaluate(features);
        if (outputs.Length < 3) return;

        Vector3 predictedForward = new Vector3(outputs[0], outputs[1], outputs[2]).normalized;
        Quaternion targetRot = Quaternion.LookRotation(predictedForward, Vector3.up);
        headTransform.rotation = Quaternion.Slerp(headTransform.rotation, targetRot, Time.deltaTime * rotationSpeed);
    }

    private float[] BuildFeatureVector(SalientObject obj)
    {
        Vector3 pos = obj.transform.position;
        Vector3 bodyForward = bodyTransform.forward;

        return new float[]
        {
            pos.x, pos.y, pos.z,
            obj.NormalizedMotion,
            obj.NormalizedAngularVelocity,
            obj.SizeByProximity,
            obj.NormalizedColorContrast,
            obj.NormalizedLuminanceContrast,
            bodyForward.x, bodyForward.y, bodyForward.z
        };
    }

    private float[] BuildSceneFeatureVector(List<SalientObject> objects)
    {
        int expectedCount = 143; // must match ONNX input size
        float[] buffer = new float[expectedCount];
        int index = 0;

        foreach (var obj in objects)
        {
            float[] objFeatures = BuildFeatureVector(obj);
            for (int i = 0; i < objFeatures.Length && index < expectedCount; i++)
                buffer[index++] = objFeatures[i];
        }

        while (index < expectedCount)
            buffer[index++] = 0f;

        return buffer;
    }

    private float[] Evaluate(float[] inputFeatures)
    {
        using (Tensor inputTensor = new Tensor(1, inputFeatures.Length, inputFeatures))
        {
            worker.Execute(inputTensor);
            Tensor output = worker.CopyOutput();
            float[] result = output.ToReadOnlyArray();
            output.Dispose();
            return result;
        }
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}
