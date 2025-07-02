using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// MCP tool for capturing Unity scene views to image files
    /// </summary>
    [McpServerToolType, Description("Scene capture tool for Unity Editor")]
    internal sealed class McpSceneCaptureTool : McpToolBase
    {
        [McpServerTool, Description("Capture scene from camera view and save as PNG")]
        public async ValueTask<string> CaptureScene(
            [Description("Name of the camera to use for capture")]
            string cameraName,
            [Description("Camera position [x,y,z] (optional)")]
            float[] position = null,
            [Description("Camera rotation in euler angles [x,y,z] (optional)")]
            float[] rotation = null,
            [Description("Capture width in pixels (optional, default: 1920)")]
            int width = 1920,
            [Description("Capture height in pixels (optional, default: 1080)")]
            int height = 1080)
        {
            GameObject tempCameraObj = null;
            return await ExecuteOperation(async () =>
            {
                // Find the camera
                Camera sourceCamera = null;
                if (!string.IsNullOrEmpty(cameraName))
                {
                    var cameraObj = McpToolUtilities.FindGameObjectInScene(cameraName);
                    if (cameraObj != null)
                        sourceCamera = cameraObj.GetComponent<Camera>();
                }

                // Fallback to main camera if not found
                if (sourceCamera == null)
                {
                    sourceCamera = Camera.main;
                    if (sourceCamera == null)
                    {
                        // Find any camera in the scene
                        sourceCamera = FindCameraInScene();
                        if (sourceCamera == null)
                            return McpToolUtilities.CreateErrorMessage("No camera found in the scene");
                    }
                }

                // Create a duplicate camera for capture
                tempCameraObj = GameObject.Instantiate(sourceCamera.gameObject);
                tempCameraObj.name = "TempCaptureCamera";
                var tempCamera = tempCameraObj.GetComponent<Camera>();

                // Set camera transform if specified, otherwise use source camera's transform
                if (position != null && position.Length >= 3)
                    tempCamera.transform.position = new Vector3(position[0], position[1], position[2]);

                if (rotation != null && rotation.Length >= 3)
                    tempCamera.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);

                // Create render texture
                var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.Create();

                // Configure temp camera for capture
                tempCamera.targetTexture = renderTexture;
                tempCamera.Render();

                // Create Texture2D and read pixels
                RenderTexture.active = renderTexture;
                var texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture2D.Apply();

                // Reset render texture
                RenderTexture.active = null;

                // Create output directory if it doesn't exist
                var projectPath = Path.GetDirectoryName(Application.dataPath);
                var outputDirectory = Path.Combine(projectPath, "SceneCapture");
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                // Generate filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                var filename = $"capture_{timestamp}.png";
                var fullPath = Path.Combine(outputDirectory, filename);

                // Save PNG
                var pngData = texture2D.EncodeToPNG();
                File.WriteAllBytes(fullPath, pngData);

                // Cleanup
                GameObject.DestroyImmediate(texture2D);
                GameObject.DestroyImmediate(renderTexture);

                LogSuccess($"Scene captured successfully using temporary camera: {fullPath}");
                
                // Always cleanup the temporary camera
                if (tempCameraObj != null)
                {
                    GameObject.DestroyImmediate(tempCameraObj);
                }
                
                return McpToolUtilities.CreateSuccessMessage($"Scene captured successfully to: {fullPath}");
            }, "capturing scene");
        }

        [McpServerTool, Description("Capture Unity Game View and save as PNG")]
        public async ValueTask<string> CaptureGameView(
            [Description("Capture width in pixels (optional, uses Game View size if not specified)")]
            int width = 0,
            [Description("Capture height in pixels (optional, uses Game View size if not specified)")]
            int height = 0,
            [Description("Use actual Game View size (optional, default: true)")]
            bool useGameViewSize = true,
            [Description("Camera position [x,y,z] (optional)")]
            float[] position = null,
            [Description("Camera rotation in euler angles [x,y,z] (optional)")]
            float[] rotation = null)
        {
            return await ExecuteOperation(async () =>
            {

                // Find main camera or any camera in the scene
                Camera sourceCamera = Camera.main;
                GameObject tempCameraCreated = null;
                
                if (sourceCamera == null)
                {
                    sourceCamera = FindCameraInScene();
                    if (sourceCamera == null)
                    {
                        // Create a temporary camera if none exists
                        tempCameraCreated = new GameObject("TempCamera");
                        sourceCamera = tempCameraCreated.AddComponent<Camera>();
                    }
                }

                // Create a duplicate camera for positioning
                GameObject duplicateCameraObj = null;
                Camera originalMainCamera = Camera.main;
                
                try
                {
                    // If position or rotation is specified, create and set up duplicate camera
                    if ((position != null && position.Length >= 3) || (rotation != null && rotation.Length >= 3))
                    {
                        duplicateCameraObj = GameObject.Instantiate(sourceCamera.gameObject);
                        duplicateCameraObj.name = "CaptureCamera_Temp";
                        var captureCamera = duplicateCameraObj.GetComponent<Camera>();

                        // Set camera transform if specified
                        if (position != null && position.Length >= 3)
                        {
                            captureCamera.transform.position = new Vector3(position[0], position[1], position[2]);
                        }

                        if (rotation != null && rotation.Length >= 3)
                        {
                            captureCamera.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                        }

                        // Set this as the main camera temporarily
                        captureCamera.tag = "MainCamera";
                        if (originalMainCamera != null && originalMainCamera != captureCamera)
                        {
                            originalMainCamera.tag = "Untagged";
                        }
                    }

                    // Create output directory if it doesn't exist
                    var projectPath = Path.GetDirectoryName(Application.dataPath);
                    var outputDirectory = Path.Combine(projectPath, "SceneCapture");
                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    // Generate filename with timestamp
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                    var filename = $"gameview_{timestamp}.png";
                    var relativePath = Path.Combine("SceneCapture", filename);
                    var fullPath = Path.Combine(outputDirectory, filename);

                    // Delete existing file if it exists
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);

                    // 撮影を実行 ※GameViewにフォーカスがない場合、この時点では撮られない
                    ScreenCapture.CaptureScreenshot(relativePath, 1);

                    // GameViewを取得
                    var assembly = typeof(UnityEditor.EditorWindow).Assembly;
                    var type = assembly.GetType("UnityEditor.GameView");
                    var gameview = EditorWindow.GetWindow(type);

                    // GameViewを再描画
                    gameview.Repaint();

                    // Wait for the screenshot to be saved
                    await UniTask.Yield();
                    
                    // Check if file was created (with timeout)
                    var timeout = DateTime.Now.AddSeconds(5);
                    while (!File.Exists(fullPath) && DateTime.Now < timeout)
                    {
                        await UniTask.Delay(100);
                    }

                    if (File.Exists(fullPath))
                    {
                        LogSuccess($"Game View captured successfully: {fullPath}");
                        return McpToolUtilities.CreateSuccessMessage($"Game View captured successfully to: {fullPath}");
                    }
                    else
                    {
                        return McpToolUtilities.CreateErrorMessage("Screenshot file was not created. Make sure Game View is visible.");
                    }
                }
                finally
                {
                    // Restore original main camera tag
                    if (originalMainCamera != null && duplicateCameraObj != null)
                    {
                        originalMainCamera.tag = "MainCamera";
                    }

                    // Cleanup duplicate camera
                    if (duplicateCameraObj != null)
                        GameObject.DestroyImmediate(duplicateCameraObj);
                        
                    // Cleanup temp camera if created
                    if (tempCameraCreated != null)
                        GameObject.DestroyImmediate(tempCameraCreated);
                }
            }, "capturing Game View");
        }

        [McpServerTool, Description("List all captured screenshots in SceneCapture folder")]
        public async ValueTask<string> ListCapturedScreenshots()
        {
            return await ExecuteOperation(async () =>
            {

                var projectPath = Path.GetDirectoryName(Application.dataPath);
                var captureDirectory = Path.Combine(projectPath, "SceneCapture");

                if (!Directory.Exists(captureDirectory))
                    return "No captures found. SceneCapture folder does not exist.";

                var files = Directory.GetFiles(captureDirectory, "*.png");
                if (files.Length == 0)
                    return "No PNG files found in SceneCapture folder.";

                var fileListBuilder = new StringBuilder("Captured screenshots:\n");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    fileListBuilder.AppendLine($"- {fileInfo.Name} (Size: {fileInfo.Length / 1024}KB, Modified: {fileInfo.LastWriteTime})");
                }

                return fileListBuilder.ToString();
            }, "listing screenshots");
        }

        [McpServerTool, Description("Capture UI Prefab in Prefab Mode and save as PNG. After capturing, Prefab mode will be closed without saving changes.")]
        public async ValueTask<string> CapturePrefabView(
            [Description("Path to the prefab asset to capture")]
            string prefabPath,
            [Description("Capture from front view automatically (optional, default: false)")]
            bool captureFromFront = false,
            [Description("Camera position [x,y,z] (optional, ignored if captureFromFront is true)")]
            float[] cameraPosition = null,
            [Description("Camera rotation in euler angles [x,y,z] (optional, ignored if captureFromFront is true)")]
            float[] cameraRotation = null,
            [Description("Capture width in pixels (optional, default: 1920)")]
            int width = 1920,
            [Description("Capture height in pixels (optional, default: 1080)")]
            int height = 1080,
            [Description("Camera distance from prefab when captureFromFront is true (optional, default: 5.0)")]
            float cameraDistance = 5.0f,
            [Description("Treat as UI object (optional, null=auto-detect, true=UI, false=3D)")]
            bool? isUIObject = null)
        {
            return await ExecuteOperation(async () =>
            {

                // If currently in Prefab mode, exit without saving
                var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (currentStage != null)
                {
                    StageUtility.GoBackToPreviousStage();
                }

                // Store current Prefab stage state
                var originalPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                string originalPrefabPath = originalPrefabStage?.assetPath;

                // Border objects for UI Canvas cleanup
                List<GameObject> borderObjects = null;

                // Verify prefab exists
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    return McpToolUtilities.CreateErrorMessage($"Prefab '{prefabPath}' not found");
                }

                // Open prefab in Prefab Mode
                var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
                if (prefabStage == null)
                {
                    return McpToolUtilities.CreateErrorMessage($"Failed to open prefab '{prefabPath}' in Prefab Mode");
                }

                try
                {
                    // Get the active Scene View
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView == null)
                    {
                        // Open a Scene View if none exists
                        sceneView = SceneView.GetWindow<SceneView>();
                    }

                    // Store original camera state
                    var originalPivot = sceneView.pivot;
                    var originalRotation = sceneView.rotation;
                    var originalSize = sceneView.size;

                    // Get prefab root
                    var prefabRoot = prefabStage.prefabContentsRoot;

                    // Calculate camera position
                    if (captureFromFront)
                    {
                        // Calculate bounds of the prefab
                        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                        bool boundsInitialized = false;
                        bool isUI = false;

                        // Determine if this is a UI object
                        Canvas canvas = null;
                        if (isUIObject.HasValue)
                        {
                            // Use explicitly specified value
                            isUI = isUIObject.Value;
                            if (isUI)
                            {
                                canvas = prefabRoot.GetComponentInParent<Canvas>() ?? prefabRoot.GetComponent<Canvas>();
                            }
                        }
                        else
                        {
                            // Auto-detect: Check if this is a UI object by looking for Canvas component
                            canvas = prefabRoot.GetComponentInParent<Canvas>();
                            if (canvas == null)
                            {
                                canvas = prefabRoot.GetComponent<Canvas>();
                            }
                            // Also check for RectTransform as a primary indicator
                            isUI = canvas != null || prefabRoot.GetComponent<RectTransform>() != null;
                        }

                        // Get all renderers in the prefab (for 3D objects)
                        var renderers = prefabRoot.GetComponentsInChildren<Renderer>();
                        foreach (var renderer in renderers)
                        {
                            if (!boundsInitialized)
                            {
                                bounds = renderer.bounds;
                                boundsInitialized = true;
                            }
                            else
                            {
                                bounds.Encapsulate(renderer.bounds);
                            }
                        }

                        // If no renderers, try RectTransforms for UI
                        if (!boundsInitialized)
                        {
                            var rectTransforms = prefabRoot.GetComponentsInChildren<RectTransform>();
                            if (rectTransforms.Length > 0)
                            {
                                // For UI objects, calculate bounds more reliably
                                // Use local bounds and transform to world space manually
                                foreach (var rectTransform in rectTransforms)
                                {
                                    if (rectTransform == null) continue;

                                    // Get rect dimensions
                                    Rect rect = rectTransform.rect;
                                    if (rect.width == 0 && rect.height == 0) continue;

                                    // Calculate world-space corners manually
                                    Vector3[] localCorners = new Vector3[4];
                                    localCorners[0] = new Vector3(rect.xMin, rect.yMin, 0); // Bottom-left
                                    localCorners[1] = new Vector3(rect.xMin, rect.yMax, 0); // Top-left
                                    localCorners[2] = new Vector3(rect.xMax, rect.yMax, 0); // Top-right
                                    localCorners[3] = new Vector3(rect.xMax, rect.yMin, 0); // Bottom-right

                                    // Transform to world space
                                    for (int i = 0; i < 4; i++)
                                    {
                                        Vector3 worldCorner = rectTransform.TransformPoint(localCorners[i]);

                                        if (!boundsInitialized)
                                        {
                                            bounds = new Bounds(worldCorner, Vector3.zero);
                                            boundsInitialized = true;
                                        }
                                        else
                                        {
                                            bounds.Encapsulate(worldCorner);
                                        }
                                    }
                                }

                                // If bounds are still too small, expand them
                                if (boundsInitialized && bounds.size.magnitude < 1f)
                                {
                                    bounds.Expand(1f);
                                }
                            }
                        }

                        // If still no bounds, create default bounds
                        if (!boundsInitialized)
                        {
                            bounds = new Bounds(prefabRoot.transform.position, Vector3.one);
                        }

                        // Debug logging for bounds calculation
                        Debug.Log($"[CapturePrefabView] Calculated bounds: Center={bounds.center}, Size={bounds.size}, IsUI={isUI}");

                        // Set camera to front view
                        sceneView.pivot = bounds.center;
                        if (isUI)
                        {
                            // For UI objects, use front view without rotation (UI faces camera by default)
                            sceneView.rotation = Quaternion.identity; // Front view for UI
                        }
                        else
                        {
                            // For 3D objects, use 180 degree rotation for front view
                            sceneView.rotation = Quaternion.Euler(0, 180, 0); // Front view for 3D
                        }

                        // Calculate appropriate camera distance
                        float requiredFieldOfView = 60f; // Standard FOV
                        float halfFOV = requiredFieldOfView * 0.5f * Mathf.Deg2Rad;

                        if (isUI)
                        {
                            // For UI objects, use GetOverlayCanvasCenterWorld to get proper world position
                            Vector3 canvasWorldCenter = Vector3.zero;

                            if (canvas != null)
                            {
                                canvasWorldCenter = GetOverlayCanvasCenterWorld(canvas, width, height);
                                // Set SceneView position to Canvas world center X,Y coordinates
                                sceneView.pivot = new Vector3(canvasWorldCenter.x, canvasWorldCenter.y, sceneView.pivot.z);
                            }
                            else
                            {
                                // Fallback to prefab root position
                                Vector3 prefabPosition = prefabRoot.transform.position;
                                sceneView.pivot = new Vector3(prefabPosition.x, prefabPosition.y, sceneView.pivot.z);
                            }

                            // Simplified size calculation for UI elements
                            // Use a reasonable size based on capture dimensions
                            float uiSize = Mathf.Max(width, height) * 0.2f; // Half of the larger dimension
                            sceneView.size = uiSize;

                            Debug.Log($"[CapturePrefabView] UI Canvas Position - Canvas={canvasWorldCenter}, Pivot={sceneView.pivot}, Size={sceneView.size}, Capture Size={width}x{height}");
                        }
                        else
                        {
                            // For 3D objects, calculate distance based on object size and desired FOV
                            float objectRadius = bounds.size.magnitude * 0.5f;
                            float distance = objectRadius / Mathf.Tan(halfFOV);

                            // The size parameter in SceneView represents half the height of the view
                            // Calculate it to ensure the entire object fits with padding
                            sceneView.size = objectRadius * 1.2f; // 20% padding
                        }

                        // Apply additional camera distance if specified
                        if (cameraDistance > 0)
                        {
                            sceneView.size += cameraDistance;
                        }
                    }
                    else
                    {
                        // Use custom camera position/rotation if provided
                        if (cameraPosition != null && cameraPosition.Length >= 3)
                        {
                            var position = new Vector3(cameraPosition[0], cameraPosition[1], cameraPosition[2]);
                            sceneView.pivot = position;
                        }

                        if (cameraRotation != null && cameraRotation.Length >= 3)
                        {
                            var rotation = Quaternion.Euler(cameraRotation[0], cameraRotation[1], cameraRotation[2]);
                            sceneView.rotation = rotation;
                        }
                    }

                    // Apply framing logic based on object type
                    Selection.activeGameObject = prefabRoot;

                    // Check if this is a UI object for proper framing
                    bool isUIForFraming = false;
                    Canvas frameCanvas = null;
                    if (isUIObject.HasValue)
                    {
                        isUIForFraming = isUIObject.Value;
                        if (isUIForFraming)
                        {
                            frameCanvas = prefabRoot.GetComponentInParent<Canvas>() ?? prefabRoot.GetComponent<Canvas>();
                        }
                    }
                    else
                    {
                        frameCanvas = prefabRoot.GetComponentInParent<Canvas>() ?? prefabRoot.GetComponent<Canvas>();
                        isUIForFraming = frameCanvas != null || prefabRoot.GetComponent<RectTransform>() != null;
                    }

                    if (isUIForFraming)
                    {
                        // For UI objects, don't use FrameSelected and keep fixed position settings
                        // Always use front view for UI (camera looking down negative Z axis)
                        sceneView.rotation = Quaternion.identity; // Front view for UI

                        // Ensure pivot uses Canvas world center position for UI capture
                        if (captureFromFront)
                        {
                            Vector3 canvasWorldCenter = Vector3.zero;
                            if (frameCanvas != null)
                            {
                                canvasWorldCenter = GetOverlayCanvasCenterWorld(frameCanvas, width, height);
                                sceneView.pivot = new Vector3(canvasWorldCenter.x, canvasWorldCenter.y, sceneView.pivot.z);
                            }
                            else
                            {
                                Vector3 prefabPosition = prefabRoot.transform.position;
                                sceneView.pivot = new Vector3(prefabPosition.x, prefabPosition.y, sceneView.pivot.z);
                            }
                        }

                        // Create border for UI Canvas if isUIObject is true
                        if (isUIObject == true && frameCanvas != null)
                        {
                            borderObjects = CreateCanvasBorder(frameCanvas, width, height);
                            Debug.Log($"[CapturePrefabView] Created {borderObjects?.Count ?? 0} border objects for Canvas");
                        }

                        Debug.Log($"[CapturePrefabView] UI Frame Settings - Pivot={sceneView.pivot}, Rotation={sceneView.rotation.eulerAngles}");
                    }
                    else
                    {
                        // Use FrameSelected for 3D objects or when not using front capture
                        sceneView.FrameSelected();

                        // Override rotation for front view if needed for 3D objects
                        if (captureFromFront && !isUIForFraming)
                        {
                            sceneView.rotation = Quaternion.Euler(0, 180, 0); // Front view for 3D
                        }
                    }

                    // Force repaint
                    sceneView.Repaint();

                    // Wait a frame for the view to update
                    await UniTask.Yield();

                    // Capture the Scene View
                    var camera = sceneView.camera;
                    if (camera == null)
                    {
                        return McpToolUtilities.CreateErrorMessage("Scene View camera not available");
                    }

                    // Create render texture
                    var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    renderTexture.Create();

                    // Store original camera settings
                    var originalTargetTexture = camera.targetTexture;
                    var originalCullingMask = camera.cullingMask;
                    var originalClearFlags = camera.clearFlags;
                    var originalBackgroundColor = camera.backgroundColor;

                    try
                    {
                        // Configure camera for clean background
                        camera.targetTexture = renderTexture;

                        // Set clear flags and background color for both UI and 3D objects
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(0.125f, 0.224f, 0.322f, 1f); // Custom background color

                        camera.Render();

                        // Create Texture2D and read pixels
                        RenderTexture.active = renderTexture;
                        var texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        texture2D.Apply();

                        // Create output directory
                        var projectPath = Path.GetDirectoryName(Application.dataPath);
                        var outputDirectory = Path.Combine(projectPath, "SceneCapture");
                        if (!Directory.Exists(outputDirectory))
                            Directory.CreateDirectory(outputDirectory);

                        // Generate filename
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                        var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                        var filename = $"prefab_{prefabName}_{timestamp}.png";
                        var fullPath = Path.Combine(outputDirectory, filename);

                        // Save PNG
                        var pngData = texture2D.EncodeToPNG();
                        File.WriteAllBytes(fullPath, pngData);

                        // Cleanup
                        GameObject.DestroyImmediate(texture2D);

                        LogSuccess($"Prefab captured successfully: {fullPath}");

                        // Restore camera state
                        sceneView.pivot = originalPivot;
                        sceneView.rotation = originalRotation;
                        sceneView.size = originalSize;
                        sceneView.Repaint();

                        return McpToolUtilities.CreateSuccessMessage($"Prefab '{prefabPath}' captured successfully to: {fullPath}");
                    }
                    finally
                    {
                        // Restore camera settings
                        camera.targetTexture = originalTargetTexture;
                        camera.cullingMask = originalCullingMask;
                        camera.clearFlags = originalClearFlags;
                        camera.backgroundColor = originalBackgroundColor;
                        RenderTexture.active = null;
                        GameObject.DestroyImmediate(renderTexture);
                    }
                }
                finally
                {
                    // Cleanup border objects if created
                    if (borderObjects != null)
                    {
                        foreach (var borderObj in borderObjects)
                        {
                            if (borderObj != null)
                            {
                                GameObject.DestroyImmediate(borderObj);
                            }
                        }
                        borderObjects.Clear();
                        Debug.Log("[CapturePrefabView] Cleaned up border objects");
                    }

                    // isUIObject=trueの場合は変更を破棄してPrefabモードを終了
                    var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (currentPrefabStage != null && isUIObject == true)
                    {
                        // 変更フラグをクリア（変更を破棄）
                        currentPrefabStage.ClearDirtiness();
                        Debug.Log("[CapturePrefabView] Cleared prefab dirtiness for UI object");
                    }

                    // Exit Prefab Mode without saving
                    StageUtility.GoBackToPreviousStage();

                    // If there was an original prefab open, reopen it
                    if (!string.IsNullOrEmpty(originalPrefabPath))
                    {
                        PrefabStageUtility.OpenPrefab(originalPrefabPath);
                    }
                }
            }, "capturing prefab view");
        }

        /// <summary>
        /// Canvas の周囲に10ピクセルの境界線を作成します。
        /// </summary>
        /// <param name="canvas">境界線を作成するCanvas</param>
        /// <param name="width">キャプチャ幅</param>
        /// <param name="height">キャプチャ高さ</param>
        /// <returns>作成された境界線GameObjectのリスト</returns>
        private static List<GameObject> CreateCanvasBorder(Canvas canvas, int width, int height)
        {
            var borderObjects = new List<GameObject>();

            try
            {
                var canvasRectTransform = canvas.transform as RectTransform;
                if (canvasRectTransform == null)
                {
                    Debug.LogWarning("[CreateCanvasBorder] Canvas does not have RectTransform");
                    return borderObjects;
                }

                // キャプチャサイズを使用
                const float borderThickness = 10f;

                // 境界線用の色（黒）
                Color borderColor = Color.black;

                // 4つの境界線を作成 (上、下、左、右)

                // 上の境界線 - Canvas幅の中央に配置、Canvas上端の外側
                var topBorder = CreateBorderImage(canvas, "TopBorder",
                    width, borderThickness,
                    new Vector2(width * 0.5f, height + borderThickness * 0.5f), borderColor);
                borderObjects.Add(topBorder);

                // 下の境界線 - Canvas幅の中央に配置、Canvas下端の外側
                var bottomBorder = CreateBorderImage(canvas, "BottomBorder",
                    width, borderThickness,
                    new Vector2(width * 0.5f, -borderThickness * 0.5f), borderColor);
                borderObjects.Add(bottomBorder);

                // 左の境界線 - Canvas左端の外側、Canvas高さ全体をカバー
                var leftBorder = CreateBorderImage(canvas, "LeftBorder",
                    borderThickness, height,
                    new Vector2(-borderThickness * 0.5f, height * 0.5f), borderColor);
                borderObjects.Add(leftBorder);

                // 右の境界線 - Canvas右端の外側、Canvas高さ全体をカバー
                var rightBorder = CreateBorderImage(canvas, "RightBorder",
                    borderThickness, height,
                    new Vector2(width + borderThickness * 0.5f, height * 0.5f), borderColor);
                borderObjects.Add(rightBorder);

                Debug.Log($"[CreateCanvasBorder] Created {borderObjects.Count} border objects for capture size: {width}x{height}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CreateCanvasBorder] Failed to create border: {e}");

                // エラーが発生した場合、既に作成されたオブジェクトをクリーンアップ
                foreach (var obj in borderObjects)
                {
                    if (obj != null)
                        GameObject.DestroyImmediate(obj);
                }
                borderObjects.Clear();
            }

            return borderObjects;
        }

        /// <summary>
        /// 境界線用のImageオブジェクトを作成します。
        /// </summary>
        /// <param name="canvas">親Canvas</param>
        /// <param name="name">オブジェクト名</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="position">位置</param>
        /// <param name="color">色</param>
        /// <returns>作成されたGameObject</returns>
        private static GameObject CreateBorderImage(Canvas canvas, string name, float width, float height, Vector2 position, Color color)
        {
            var borderObj = new GameObject(name);
            borderObj.transform.SetParent(canvas.transform, false);

            // RectTransformの設定
            var rectTransform = borderObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, height);
            rectTransform.anchoredPosition = position;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;

            // Imageコンポーネントの追加と設定
            var image = borderObj.AddComponent<Image>();
            image.color = color;

            // 最前面に表示されるようにソート順序を設定
            var canvasGroup = borderObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            return borderObj;
        }

        /// <summary>
        /// Prefab モード中の Screen Space – Overlay Canvas の中心を
        /// 一時的なGameObjectを配置して取得したワールド座標で返します。
        /// </summary>
        /// <param name="canvas">対象のCanvas</param>
        /// <param name="width">キャプチャ幅</param>
        /// <param name="height">キャプチャ高さ</param>
        private static Vector3 GetOverlayCanvasCenterWorld(Canvas canvas, int width, int height)
        {
            return new Vector3(width / 4f, height / 4f, 0f);

            // // ① Canvas の中心 (UI 座標) を求める
            // RectTransform rt = canvas.transform as RectTransform;
            // Vector3 uiCenter = new(rt.rect.width * 0.5f, rt.rect.height * 0.5f, 0f);

            // // ② Canvas中心位置に空のGameObjectを一時的に配置
            // var tempGameObject = new GameObject("TempCanvasCenter");
            // tempGameObject.transform.SetParent(canvas.transform, false);

            // // RectTransformとして設定してCanvas中心に配置
            // var tempRectTransform = tempGameObject.AddComponent<RectTransform>();
            // tempRectTransform.anchoredPosition = new Vector2(uiCenter.x, uiCenter.y);
            // tempRectTransform.anchorMin = Vector2.zero;
            // tempRectTransform.anchorMax = Vector2.zero;

            // // ③ 配置したGameObjectのワールド座標を記憶
            // Vector3 worldPosition = tempGameObject.transform.position;

            // // ④ 配置したGameObjectを削除
            // GameObject.DestroyImmediate(tempGameObject);

            // // ⑤ 記憶したワールド座標を返却
            // return worldPosition;
        }


        private Camera FindCameraInScene()
        {
            // Use Resources.FindObjectsOfTypeAll to find both active and inactive cameras
            var allCameras = Resources.FindObjectsOfTypeAll<Camera>();
            
            foreach (var camera in allCameras)
            {
                // Skip cameras not in the active scene
                if (camera.gameObject.scene != EditorSceneManager.GetActiveScene())
                    continue;
                
                // Skip persistent objects (prefab assets)
                if (EditorUtility.IsPersistent(camera.gameObject))
                    continue;
                
                return camera;
            }
            
            return null;
        }
    }
}