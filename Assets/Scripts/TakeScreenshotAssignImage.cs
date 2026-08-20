using UnityEngine;

public class TakeScreenshotAssignImage : MonoBehaviour
{
    void OnMouseDown()
    {
        ScreenCapture.CaptureScreenshot("Screenshot.png");
    }
}
