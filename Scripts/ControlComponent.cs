using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.EventSystems;
using System;
using System.Text.RegularExpressions;
using PowerCraft.Utilities;
using PowerCraft.Scenes.Main;
using PowerCraft.Entities.Boxel;

public class ControlComponent : MonoBehaviour {
  [SerializeField] Transform _cameraCenter;
  [SerializeField] Transform _gizmo;
  [SerializeField] List<GameObject> _hideMenus;
  [SerializeField] float _defaultDistance;
  [SerializeField] float _moveSensitibity;
  [SerializeField] float _rotateSensitibity;
  [SerializeField] float _zoomSensitibity;
  [SerializeField] float _gizmoMoveSensitibity;
  [SerializeField] float _gizmoRotateSensitibity;
  [SerializeField] float _gizmoScaleSensitibity;
  [SerializeField] WorldModel _world;

  public Action OnMoveObjectListener;

  Camera _camera;
  float preMagnitude;
  bool _isMoving;
  bool _isZooming;
  GameObject _currentTarget;
  GameObject _currentGizmo;
  Vector3 _hitVector;
  bool _onUI;

  void Awake() {
    _camera = Camera.main;

    Touch.onFingerDown += OnFingerDown;
    Touch.onFingerMove += OnFingerMove;
    Touch.onFingerUp += OnFingerUp;

    transform.LookAt(_cameraCenter.position);
    transform.position = (transform.position - _cameraCenter.position).normalized * _defaultDistance + _cameraCenter.position;

    _onUI = false;
    _hitVector = Vector3.zero;

    _gizmo.GetComponent<AxisTypeComponent>().OnChangeAxisType += SetGizmoAxis;
    _world.OnActivatePlayMode += UnsetTarget;
  }

  void OnFingerDown(Finger finger) {
    _isMoving = false;
    _isZooming = false;
    _onUI = IsPointerOverUIObject();
    if (!_onUI) OnTouch();
  }

  void OnFingerMove(Finger finger) {
    if (!_onUI) OnTouch();
  }

  void OnFingerUp(Finger finger) {
    _onUI = false;
    _currentGizmo = null;
    SetGizmoAxis();
  }

  public void SetGizmoAxis() {
    if (_currentTarget != null) _gizmo.position = _currentTarget.transform.position;
    if (_gizmo.GetComponent<AxisTypeComponent>().IsGlobal) {
      _gizmo.localRotation = Quaternion.identity;
    } else if (_currentTarget != null) {
      _gizmo.localRotation = _currentTarget.transform.localRotation;
    }
  }

  void OnTouch() {
    foreach (var hideMenu in _hideMenus) hideMenu.SetActive(false);
    var touches = Touch.activeTouches;
    if (touches.Count == 1) {
      SingleTouch(touches[0]);
    } else if (touches.Count == 2) {
      DoubleTouch(touches[0], touches[1]);
    }
  }

  void SingleTouch(Touch primaryTouch) {
    var ray = _camera.ScreenPointToRay(primaryTouch.screenPosition);
    var hit = new RaycastHit();
    var hitGizmo = new RaycastHit();
    int layerMask = LayerMask.GetMask(new string[] { "Gizmo" });
    var isHitGizmo = Physics.Raycast(ray, out hitGizmo, 100f, layerMask);
    var primaryDelta = primaryTouch.delta;
    if (!_isMoving && (Physics.Raycast(ray, out hit, 100f) || isHitGizmo)) {
      if (isHitGizmo) {
        _hitVector = hitGizmo.point - _gizmo.position;
        _currentGizmo = hitGizmo.collider.gameObject;
        MoveObject(primaryDelta);
      } else {
        Tap(hit.collider.gameObject);
      }
    } else {
      if (_currentGizmo != null) {
        MoveObject(primaryDelta);
      } else {
        UnsetTarget();
        Move(primaryDelta);
      }
    }
  }

  void DoubleTouch(Touch primaryTouch, Touch secondaryTouch) {
    var primaryDelta = primaryTouch.delta;
    var secondaryDelta = secondaryTouch.delta;
    if ((secondaryDelta - primaryDelta).magnitude + 1f >= primaryDelta.magnitude) {
      Zoom((secondaryTouch.screenPosition - primaryTouch.screenPosition).magnitude);
    } else {
      Rotate(primaryDelta);
    }
  }

  void MoveObject(Vector2 deltaScreen) {
    _isMoving = true;
    _isZooming = false;
    var delta = transform.TransformVector(Vector3.right) * deltaScreen.x + transform.TransformVector(Vector3.up) * deltaScreen.y;
    var right = _gizmo.TransformVector(Vector3.right);
    var up = _gizmo.TransformVector(Vector3.up);
    var forward = _gizmo.TransformVector(Vector3.forward);
    var name = _currentGizmo.name;
    if (Regex.IsMatch(name, "^Move")) {
      var deltaPosition = Vector3.zero;
      if (name == "MoveXAxis" || name == "MoveXAxis.Collider" || name == "MoveXHead") {
        deltaPosition = Vector3.Dot(right, delta) * right * _gizmoMoveSensitibity;
      } else if (name == "MoveYAxis" || name == "MoveYAxis.Collider" || name == "MoveYHead") {
        deltaPosition = Vector3.Dot(up, delta) * up * _gizmoMoveSensitibity;
      } else if (name == "MoveZAxis" || name == "MoveZAxis.Collider" || name == "MoveZHead") {
        deltaPosition = Vector3.Dot(forward, delta) * forward * _gizmoMoveSensitibity;
      } else if (name == "MoveXPlane1" || name == "MoveXPlane2" || name == "MoveXPlane3" || name == "MoveXPlane4") {
        deltaPosition = ((Vector3.Dot(up, delta) * up) + Vector3.Dot(forward, delta) * forward) * _gizmoMoveSensitibity / 2f;
      } else if (name == "MoveYPlane1" || name == "MoveYPlane2" || name == "MoveYPlane3" || name == "MoveYPlane4") {
        deltaPosition = ((Vector3.Dot(right, delta) * right) + Vector3.Dot(forward, delta) * forward) * _gizmoMoveSensitibity / 2f;
      } else if (name == "MoveZPlane1" || name == "MoveZPlane2" || name == "MoveZPlane3" || name == "MoveZPlane4") {
        deltaPosition = ((Vector3.Dot(right, delta) * right) + Vector3.Dot(up, delta) * up) * _gizmoMoveSensitibity / 2f;
      }
      _currentTarget.transform.position += deltaPosition;
      _gizmo.position += deltaPosition;
    } else if (Regex.IsMatch(name, "^Rotate")) {
      var axis = Vector3.zero;
      if (name == "RotateXHundle" || name == "RotateXHundle.Collider") {
        axis = right;
      } else if (name == "RotateYHundle" || name == "RotateYHundle.Collider") {
        axis = up;
      } else if (name == "RotateZHundle" || name == "RotateZHundle.Collider") {
        axis = forward;
      }
      var deltaRotation = Vector3.Dot(Vector3.Cross(axis, _hitVector), delta) * _gizmoRotateSensitibity;
      _currentTarget.transform.rotation = Quaternion.AngleAxis(deltaRotation, axis) * _currentTarget.transform.rotation;
      _gizmo.rotation = Quaternion.AngleAxis(deltaRotation, axis) * _gizmo.rotation;
    } else if (Regex.IsMatch(name, "^Scale")) {
      var deltaScale = Vector3.zero;
      if (name == "ScaleHundle") {
        var scale = Vector2.Dot(new Vector2(1f, 1f), deltaScreen) * _gizmoScaleSensitibity / 3f;
        deltaScale = new Vector3(1f + scale, 1f + scale, 1f + scale);
      } else if (name == "ScaleXAxis" || name == "ScaleXAxis.Collider" || name == "ScaleXHundle") {
        var scale = Vector3.Dot(right, delta) * _gizmoScaleSensitibity;
        deltaScale = new Vector3(1f + scale, 1f, 1f);
      } else if (name == "ScaleYAxis" || name == "ScaleYAxis.Collider" || name == "ScaleYHundle") {
        var scale = Vector3.Dot(up, delta) * _gizmoScaleSensitibity;
        deltaScale = new Vector3(1f, 1f + scale, 1f);
      } else if (name == "ScaleZAxis" || name == "ScaleZAxis.Collider" || name == "ScaleZHundle") {
        var scale = Vector3.Dot(forward, delta) * _gizmoScaleSensitibity;
        deltaScale = new Vector3(1f, 1f, 1f + scale);
      }
      var newScale = _currentTarget.transform.localScale;
      newScale.x *= deltaScale.x;
      newScale.y *= deltaScale.y;
      newScale.z *= deltaScale.z;
      _currentTarget.transform.localScale = newScale;
    }
    SystemUtility.SafeCall(OnMoveObjectListener);
  }

  void Tap(GameObject target) {
    if (!target.CompareTag("Boxel")) return;
    SetTarget(target);
    target.GetComponent<SelectComponent>().OnSelected();
  }

  void SetTarget(GameObject target) {
    if (_world.EditMode && _world.BoxelEditMode) {
      if (target == _currentTarget) {
        _cameraCenter.position = _currentTarget.transform.position;
        transform.LookAt(_cameraCenter.position);
        transform.position = (transform.position - _cameraCenter.position).normalized * _defaultDistance + _cameraCenter.position;
      }
      _currentTarget = target;
      SetGizmoAxis();
      _gizmo.GetComponent<VisibilityComponent>().ShowGizmo();
    }
  }

  public void UnsetTarget() {
    _currentTarget = null;
    if (_world.PlayMode || _world.BoxelEditMode) _world.ResetActiveBoxel();
    _gizmo.GetComponent<VisibilityComponent>().HideGizmo();
  }

  void Rotate(Vector2 deltaScreen) {
    _isMoving = true;
    _isZooming = false;
    transform.RotateAround(_cameraCenter.position, Vector3.up, deltaScreen.x * _rotateSensitibity);
    transform.RotateAround(_cameraCenter.position, transform.TransformVector(Vector3.left), deltaScreen.y * _rotateSensitibity);
  }

  void Zoom(float magnitude) {
    if (_isZooming) transform.position = transform.position + transform.TransformVector(Vector3.forward) * (magnitude / preMagnitude - 1) * _zoomSensitibity;
    _isMoving = true;
    _isZooming = true;
    preMagnitude = magnitude;
  }

  void Move(Vector2 deltaScreen) {
    _isMoving = true;
    _isZooming = false;
    var deltaPosition = (transform.TransformVector(Vector3.left) * deltaScreen.x + transform.TransformVector(Vector3.down) * deltaScreen.y) * _moveSensitibity;
    var newPos = _cameraCenter.position + deltaPosition;
    if (Math.Abs(newPos.x) < 50f && newPos.y > -1.6f && newPos.y < 20f && Math.Abs(newPos.z) < 50f) {
      transform.position += deltaPosition;
      _cameraCenter.position += deltaPosition;
    }
  }

  protected void OnEnable() {
    EnhancedTouchSupport.Enable();
  }
  protected void OnDisable() {
    EnhancedTouchSupport.Disable();
  }

  static bool IsPointerOverUIObject() {
    PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
    eventDataCurrentPosition.position = Touch.activeTouches[0].screenPosition;

    var raycastResults = new List<RaycastResult>();
    EventSystem.current.RaycastAll(eventDataCurrentPosition, raycastResults);
    var over = raycastResults.Count > 0;
    raycastResults.Clear();
    return over;
  }
}
