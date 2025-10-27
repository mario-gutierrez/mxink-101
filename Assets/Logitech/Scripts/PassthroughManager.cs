using UnityEngine;
public class PassthroughManager : MonoBehaviour
{
    [SerializeField] private OVRPassthroughLayer _passthroughLayer;
    private bool _yButtonIsPressed = false;

    void Start()
    {
        _passthroughLayer.enabled = true;
        // Set camera background to transparent
        OVRCameraRig ovrCameraRig = GameObject.Find("OVRCameraRig").GetComponent<OVRCameraRig>();
        var centerCamera = ovrCameraRig.centerEyeAnchor.GetComponent<Camera>();
        centerCamera.clearFlags = CameraClearFlags.SolidColor;
        centerCamera.backgroundColor = Color.clear;
    }

    public void TogglePassthrough()
    {
        OVRCameraRig ovrCameraRig = GameObject.Find("OVRCameraRig").GetComponent<OVRCameraRig>();
        var centerCamera = ovrCameraRig.centerEyeAnchor.GetComponent<Camera>();
        //centerCamera.clearFlags = CameraClearFlags.SolidColor;
        centerCamera.backgroundColor = _passthroughLayer.enabled ? Color.gray : Color.clear;
        _passthroughLayer.enabled = !_passthroughLayer.enabled;
    }
    // Update is called once per frame
    void Update()
    {
        if (OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch))
        {
            if (!_yButtonIsPressed)
            {
                TogglePassthrough();
            }
            _yButtonIsPressed = true;
        }
        else
        {
            _yButtonIsPressed = false;
        }
    }
}
