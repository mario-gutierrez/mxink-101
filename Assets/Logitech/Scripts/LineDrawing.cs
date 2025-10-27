using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class LineDrawing : MonoBehaviour
{
    private List<GameObject> _lines = new List<GameObject>();
    private LineRenderer _currentLine;
    private List<float> _currentLineWidths = new List<float>(); //list to store line widths

    [SerializeField] float _maxLineWidth = 0.01f;
    [SerializeField] float _minLineWidth = 0.0005f;

    [SerializeField] Material _material;

    [SerializeField] private Color _currentColor;
    [SerializeField] private Color highlightColor;
    private const float MIN_HIGHLIGHT_THRESHOLD = 0.005f;
    private const float DEFAULT_HIGHLIGHT_THRESHOLD = 0.01f;
    private const float MAX_HIGHLIGHT_THRESHOLD = 0.2f;
    private float _highlightThreshold = DEFAULT_HIGHLIGHT_THRESHOLD;
    [SerializeField] private GameObject _selectionSphere;
    private List<LineData> _highlightedLines;

    public class LineData
    {
        public Vector3 StartGrabPosition;
        public Quaternion StartGrabRotation;
        public Vector3[] InitialPositions;
        public Color OriginalColor;
        public LineRenderer LineRenderer;
        public GameObject LineGameObject;
        public LineData(GameObject lineGameObject, Color highlightColor)
        {
            LineGameObject = lineGameObject;
            LineRenderer = lineGameObject.GetComponent<LineRenderer>();
            OriginalColor = LineRenderer.material.color;
            LineRenderer.material.color = highlightColor;
            InitialPositions = new Vector3[LineRenderer.positionCount];
            LineRenderer.GetPositions(InitialPositions);
        }
    }
    private bool _movingLine = false;
    public Color CurrentColor
    {
        get { return _currentColor; }
        set
        {
            _currentColor = value;
        }
    }

    public float MaxLineWidth
    {
        get { return _maxLineWidth; }
        set { _maxLineWidth = value; }
    }

    private bool _lineWidthIsFixed = false;
    public bool LineWidthIsFixed
    {
        get { return _lineWidthIsFixed; }
        set { _lineWidthIsFixed = value; }
    }

    private bool _isDrawing = false;
    private bool _doubleTapDetected = false;

    [SerializeField]
    private float longPressDuration = 1.0f;
    private float buttonPressedTimestamp = 0;

    [SerializeField]
    private StylusHandler _stylusHandler;

    private Vector3 _previousLinePoint;
    private const float _minDistanceBetweenLinePoints = 0.0005f;

    private void Start()
    {
        _highlightedLines = new List<LineData>();
    }

    private void StartNewLine()
    {
        var gameObject = new GameObject("line");
        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
        _currentLine = lineRenderer;
        _currentLine.positionCount = 0;
        _currentLine.material = _material;
        _currentLine.material.color = _currentColor;
        _currentLine.loop = false;
        _currentLine.startWidth = _minLineWidth;
        _currentLine.endWidth = _minLineWidth;
        _currentLine.useWorldSpace = true;
        _currentLine.alignment = LineAlignment.View;
        _currentLine.widthCurve = new AnimationCurve();
        _currentLineWidths = new List<float>();
        _currentLine.shadowCastingMode = ShadowCastingMode.Off;
        _currentLine.receiveShadows = false;
        _lines.Add(gameObject);
        _previousLinePoint = new Vector3(0, 0, 0);
    }

    private void AddPoint(Vector3 position, float width)
    {
        if (Vector3.Distance(position, _previousLinePoint) > _minDistanceBetweenLinePoints)
        {
            TriggerHaptics();
            _previousLinePoint = position;
            _currentLine.positionCount++;
            _currentLineWidths.Add(Math.Max(width * _maxLineWidth, _minLineWidth));
            _currentLine.SetPosition(_currentLine.positionCount - 1, position);

            //create a new AnimationCurve
            AnimationCurve curve = new AnimationCurve();

            //populate the curve with keyframes based on the widths list

            for (var i = 0; i < _currentLineWidths.Count; i++)
            {
                curve.AddKey(i / (float)(_currentLineWidths.Count - 1),
                 _currentLineWidths[i]);
            }

            //assign the curve to the widthCurve
            _currentLine.widthCurve = curve;
        }
    }

    private void RemoveLastLine()
    {
        GameObject lastLine = _lines[_lines.Count - 1];
        _lines.RemoveAt(_lines.Count - 1);

        Destroy(lastLine);
    }

    private void ClearAllLines()
    {
        foreach (var line in _lines)
        {
            Destroy(line);
        }
        _lines.Clear();
        _highlightedLines.Clear();
        _movingLine = false;
    }

    private void TriggerHaptics()
    {
        const float dampingFactor = 0.4f;
        const float duration = 0.01f;
        float middleButtonPressure = _stylusHandler.CurrentState.cluster_middle_value * dampingFactor;

        try
        {
            ((VrStylusHandler)_stylusHandler).TriggerHapticPulse(middleButtonPressure, duration);
        }
        catch (Exception e)
        {
            // Catch exception when stylus is a mockup (StylusHandler super class)
            Debug.Log(e);
        }
    }
    void AdjustHighlightThreshold()
    {

        var ndhController = _stylusHandler.CurrentState.isOnRightHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        bool showSelectionSphere = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, ndhController);

        _selectionSphere.SetActive(showSelectionSphere);
        float thumbstickX = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ndhController).x;
        _highlightThreshold = DEFAULT_HIGHLIGHT_THRESHOLD;
        if (thumbstickX <= 0)
        {
            _highlightThreshold = Math.Max(MIN_HIGHLIGHT_THRESHOLD, (1 + thumbstickX) * DEFAULT_HIGHLIGHT_THRESHOLD);
        }
        else
        {
            _highlightThreshold = DEFAULT_HIGHLIGHT_THRESHOLD + MAX_HIGHLIGHT_THRESHOLD * thumbstickX;
        }


        float scaleFactor = _highlightThreshold * 2.0f;
        _selectionSphere.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
    }
    void Update()
    {
        AdjustHighlightThreshold();
        float analogInput = Mathf.Max(_stylusHandler.CurrentState.tip_value, _stylusHandler.CurrentState.cluster_middle_value);
        if (!_stylusHandler.CurrentState.cluster_front_value && analogInput > 0 && _stylusHandler.CanDraw())
        {
            UnhighlightLines();

            if (!_isDrawing)
            {
                StartNewLine();
                _isDrawing = true;
            }
            AddPoint(_stylusHandler.CurrentState.inkingPose.position, _lineWidthIsFixed ? 1.0f : analogInput);
            return;
        }
        else
        {
            _isDrawing = false;
        }

        //Undo by double tapping or clicking on cluster_back button on stylus
        if (_stylusHandler.CurrentState.cluster_back_double_tap_value ||
        _stylusHandler.CurrentState.cluster_back_value)
        {
            if (_lines.Count > 0 && !_doubleTapDetected)
            {
                _doubleTapDetected = true;
                buttonPressedTimestamp = Time.time;

                if (_highlightedLines.Count > 0)
                {
                    foreach (LineData line in _highlightedLines)
                    {
                        _lines.Remove(line.LineGameObject);
                        Destroy(line.LineGameObject);
                    }
                    _highlightedLines.Clear();
                }
                else
                {
                    // if no lines are selected, delete the most recent one
                    RemoveLastLine();
                }


                //haptic click when deleting lines
                ((VrStylusHandler)_stylusHandler).TriggerHapticClick();
                return;

            }
            else if (_lines.Count > 0 && Time.time >= (buttonPressedTimestamp + longPressDuration))
            {
                //haptic pulse when removing all lines
                ((VrStylusHandler)_stylusHandler).TriggerHapticPulse(1.0f, 0.1f);
                ClearAllLines();
                return;
            }
        }
        else
        {
            _doubleTapDetected = false;
        }

        // Look for closest Line
        if (!_movingLine)
        {
            UnhighlightLines();
            FindClosestLines(_stylusHandler.CurrentState.inkingPose.position);
        }
        if (_stylusHandler.CurrentState.cluster_front_value && !_movingLine)
        {
            _movingLine = true;
            StartGrabbingLine();
        }
        else if (!_stylusHandler.CurrentState.cluster_front_value && _movingLine)
        {
            _movingLine = false;
        }
        else if (_stylusHandler.CurrentState.cluster_front_value)
        {
            MoveHighlightedLines();
        }
    }

    private void FindClosestLines(Vector3 position)
    {
        foreach (var line in _lines)
        {
            var lineRenderer = line.GetComponent<LineRenderer>();
            for (var i = 0; i < lineRenderer.positionCount - 1; i++)
            {
                var point = FindNearestPointOnLineSegment(lineRenderer.GetPosition(i),
                    lineRenderer.GetPosition(i + 1), position);
                var distance = Vector3.Distance(point, position);
                if (distance < _highlightThreshold)
                {
                    _highlightedLines.Add(new LineData(line, highlightColor));
                    break;
                }
            }
        }
    }
    private Vector3 FindNearestPointOnLineSegment(Vector3 segStart, Vector3 segEnd, Vector3 point)
    {
        var segVec = segEnd - segStart;
        var segLen = segVec.magnitude;
        var segDir = segVec.normalized;

        var pointVec = point - segStart;
        var projLen = Vector3.Dot(pointVec, segDir);
        var clampedLen = Mathf.Clamp(projLen, 0f, segLen);

        return segStart + segDir * clampedLen;
    }

    private void UnhighlightLines()
    {
        foreach (LineData line in _highlightedLines)
        {
            var lineRenderer = line.LineRenderer;
            lineRenderer.material.color = line.OriginalColor;
        }
        if (_highlightedLines.Count > 0)
        {
            _movingLine = false;
        }
        _highlightedLines.Clear();
    }

    private void StartGrabbingLine()
    {
        foreach (LineData line in _highlightedLines)
        {
            line.StartGrabPosition = _stylusHandler.CurrentState.inkingPose.position;
            line.StartGrabRotation = _stylusHandler.CurrentState.inkingPose.rotation;
        }
        // haptic pulse when start grabbing a line
        ((VrStylusHandler)_stylusHandler).TriggerHapticPulse(1.0f, 0.03f);
    }

    private void MoveHighlightedLines()
    {
        foreach (LineData line in _highlightedLines)
        {
            var rotation = _stylusHandler.CurrentState.inkingPose.rotation * Quaternion.Inverse(line.StartGrabRotation);
            var newPositions = new Vector3[line.InitialPositions.Length];

            for (var i = 0; i < line.InitialPositions.Length; i++)
            {
                newPositions[i] = rotation * (line.InitialPositions[i] - line.StartGrabPosition) + _stylusHandler.CurrentState.inkingPose.position;
            }

            line.LineRenderer.SetPositions(newPositions);
        }
    }
}
