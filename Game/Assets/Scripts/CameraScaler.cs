using UnityEngine;
using UnityEngine.UI;

public class CameraScaler : MonoBehaviour
{
    [Header("Canvas to Scale")]
    [SerializeField] private Canvas targetCanvas;
    
    [Header("Reference Resolution")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    
    private Camera cam;
    private CanvasScaler canvasScaler;
    
    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        if (targetCanvas != null)
        {
            SetupCanvas();
        }
    }
    
    private void Start()
    {
        AdjustCameraToCanvas();
    }
    
    private void SetupCanvas()
    {
        // Get or add CanvasScaler component
        canvasScaler = targetCanvas.GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            canvasScaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        }
        
        // Configure CanvasScaler to scale with screen size
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f; // Balance between width and height
        
        // Set canvas to use this camera
        targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        targetCanvas.worldCamera = cam;
        
        // Set canvas plane distance
        targetCanvas.planeDistance = 10f;
    }
    
    private void AdjustCameraToCanvas()
    {
        if (cam == null || !cam.orthographic || targetCanvas == null) return;
        
        // Calculate orthographic size based on reference resolution
        // This makes the camera view match the canvas reference resolution
        float referenceAspect = referenceResolution.x / referenceResolution.y;
        float screenAspect = (float)Screen.width / (float)Screen.height;
        
        // Base orthographic size (adjust this to zoom in/out)
        float baseSize = 5f;
        
        // Adjust orthographic size to maintain aspect ratio
        if (screenAspect > referenceAspect)
        {
            // Screen is wider - use base size
            cam.orthographicSize = baseSize;
        }
        else
        {
            // Screen is taller - scale up to fit width
            cam.orthographicSize = baseSize * (referenceAspect / screenAspect);
        }
        
        // Set camera background to clear
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
    }
}
