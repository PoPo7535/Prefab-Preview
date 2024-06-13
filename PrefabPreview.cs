using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

public static class PrefabPreview
{
    private const int defaultTextureWidth = 256;
    private const int defaultTextureHeight = 256;
    private static readonly Vector3 defaultPosition = new Vector3(0, 1000, 0);
    private static readonly Vector3 defaultRotation = new Vector3(26, 135, -24);
    private static readonly Vector3 defaultScale = new Vector3(1, 1, 1);
    private static readonly Color defaultColor = new Color(0.94f, 0.94f, 0.94f);
    private static Camera camera;
    static PrefabPreview()
    {
        var cameraObject = new GameObject("PrefabPreviewCamera");
        var cameraComponet = cameraObject.AddComponent<Camera>();
        cameraComponet.clearFlags = CameraClearFlags.SolidColor;
        camera = cameraComponet;
        camera.nearClipPlane = 0.01f;
    }
    
    public static Sprite GetPrefabPreView(GameObject prefab, Color backGroundColor = default)
    {
        var result = PrefabUtility.GetPrefabAssetType(prefab);
        if (result == PrefabAssetType.NotAPrefab)
        {
            Debug.LogWarning($"{nameof(PrefabPreview)} : is Not Prefab");  
            return null;
        }

        if (null != prefab.transform.parent)
        {
            Debug.LogWarning($"{nameof(PrefabPreview)} : is Not Parent");  
            return null;
        }
        
        backGroundColor = backGroundColor == default ? defaultColor : backGroundColor;
        
        camera.backgroundColor = backGroundColor;
        var texture2D = GetPrefabPreview(prefab, camera, defaultPosition, Quaternion.Euler(defaultRotation), defaultScale, 1);
        Sprite sprite = null;
        if (texture2D != null)
        {
            sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
        }
        return sprite;
    }

    private static Texture2D GetPrefabPreview(GameObject prefab, Camera Camera, Vector3 position, Quaternion rotation,
        Vector3 scale, float previewScale, bool instantiate = true)
    {

        bool wasActive = prefab.activeSelf;
        Vector3 prevPosition = prefab.transform.position;
        Vector3 prevEuler = prefab.transform.eulerAngles;
        Vector3 prevScale = prefab.transform.localScale;
        int prevLayer = prefab.layer;

        GameObject go;
        Renderer[] renderers;
        Transform prevParent = null;

        if (instantiate)
        {
            prefab.SetActive(false);

            go = Object.Instantiate(prefab, position, rotation * Quaternion.Inverse(prefab.transform.rotation));
            if (true)
            {   
                MonoBehaviour[] scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < scripts.Length; ++i)
                {
                    if (scripts[i] == null)
                    {
                        continue;
                    }

                    if (scripts[i].GetType().FullName.StartsWith("UnityEngine"))
                    {
                        continue;
                    }

                    Object.DestroyImmediate(scripts[i]);
                }
            }

            prefab.SetActive(wasActive);
            renderers = go.GetComponentsInChildren<Renderer>(false);
        }
        else
        {
            go = prefab;
            go.SetActive(true);

            prevParent = go.transform.parent;
            go.transform.SetParent(null, false);
            go.transform.position = position;
            go.transform.rotation = rotation;

            renderers = go.GetComponentsInChildren<Renderer>(false);
        }

    
        Texture2D texture = null;
        if (renderers.Length != 0)
        {
            go.transform.localScale = scale;
            Bounds bounds = CalculateBounds(go.transform);
            float fov = Camera.fieldOfView * Mathf.Deg2Rad;
            float objSize = Mathf.Max(bounds.extents.y, bounds.extents.x, bounds.extents.z);
            float distance = Mathf.Abs(objSize / Mathf.Sin(fov / 2.0f));
            go.SetActive(true);
            foreach (var t in renderers)
            {
                t.gameObject.SetActive(true);
            }
            // position += bounds.center; // ??
            Camera.transform.position = bounds.center - distance * Camera.transform.forward;
            Camera.orthographicSize = objSize;
            SetLayerRecursively(go, 0);
            Camera.targetTexture = RenderTexture.GetTemporary(defaultTextureWidth, defaultTextureHeight, 24);
            Camera.enabled = true;
            Camera.Render();
            Camera.enabled = false;
            RenderTexture saveActive = RenderTexture.active;
        
            var targetTexture = Camera.targetTexture;
            RenderTexture.active = targetTexture;
            texture = new Texture2D(targetTexture.width, targetTexture.height);
            texture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = saveActive;
            RenderTexture.ReleaseTemporary(Camera.targetTexture);
        }
        if (instantiate)
        {
            Object.DestroyImmediate(go); 
        }
        else
        {
            go.SetActive(wasActive);
            go.transform.SetParent(prevParent, false);
            go.transform.position = prevPosition;
            go.transform.eulerAngles = prevEuler;
            go.transform.localScale = prevScale;
            SetLayerRecursively(go, prevLayer);
        }
        return texture;
    }

    private static void SetLayerRecursively(GameObject o, int layer)
    {
        foreach (var t in o.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
        }
    }

    private static Bounds CalculateBounds(Transform t, bool includeInactive = false)
    {
        var renderer = t.GetComponentInChildren<Renderer>(includeInactive);
    
        if (null == renderer) 
            return new Bounds(t.position, new Vector3(0.5f, 0.5f, 0.5f));

        if (false == t.gameObject)
        {
        
        }
    
        var bounds = renderer.bounds;
        if (bounds.size == Vector3.zero && bounds.center != renderer.transform.localPosition)
        {
            bounds = TransformBounds(renderer.transform.localToWorldMatrix, bounds);
        }
        CalculateBounds(t, ref bounds);
        if (bounds.extents == Vector3.zero)
        {
            bounds.extents = new Vector3(0.5f, 0.5f, 0.5f);
        }
        return bounds;

    }

    private static void CalculateBounds(Transform t, ref Bounds totalBounds)
    {
        foreach (Transform child in t)
        {
            var renderer = child.GetComponent<Renderer>();
            if (renderer)
            {
                var bounds = renderer.bounds;
                if (bounds.size == Vector3.zero && bounds.center != renderer.transform.position)
                {
                    bounds = TransformBounds(renderer.transform.localToWorldMatrix, bounds);
                }

                totalBounds.Encapsulate(bounds.min);
                totalBounds.Encapsulate(bounds.max);
            }

            CalculateBounds(child, ref totalBounds);
        }
    }
    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        return TransformBounds(ref matrix, ref bounds);
    }
    private static Bounds TransformBounds(ref Matrix4x4 matrix, ref Bounds bounds)
    {
        var center = matrix.MultiplyPoint(bounds.center);

        // transform the local extents' axes
        var extents = bounds.extents;
        var axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
        var axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
        var axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));

        // sum their absolute value to get the world extents
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds { center = center, extents = extents };
    }
}

