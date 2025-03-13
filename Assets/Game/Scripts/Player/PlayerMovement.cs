using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private InputManager _input;
    [SerializeField] private float _walkSpeed;
    [SerializeField] private float _sprintSpeed;
    [SerializeField] private float _walkSprintTransition;
    [SerializeField] private float _rotationSmoothTime = 0.1f;
    [SerializeField] private float _jumpForce;
    [SerializeField] private float _crouchSpeed;

    [Header("Climb")]
    [SerializeField] private Transform _climbDetector;
    [SerializeField] private float _climbCheckDistance;
    [SerializeField] private LayerMask _climbableLayer;
    [SerializeField] private Vector3 _climbOffset;
    [SerializeField] private float _climbSpeed;

    [Header("Glide")]
    [SerializeField] private float _glideSpeed;
    [SerializeField] private float _airDrag;
    [SerializeField] private Vector3 _glideRotationSpeed;
    [SerializeField] private float _minGlideRotationX;
    [SerializeField] private float _maxGlideRotationX;

    [Header("Attack")]
    [SerializeField] private Transform _hitDetector;
    [SerializeField] private float _hitDetectorRadius;
    [SerializeField] private LayerMask _hitLayer;
    [SerializeField] private float _resetComboInterval;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundDetector;
    [SerializeField] private float _detectorRadius;
    [SerializeField] private LayerMask _detectorLayer;

    [Header("Step Check")]
    [SerializeField] private Vector3 _upperStepOffset;
    [SerializeField] private float _stepCheckerDistance;
    [SerializeField] private float _stepForce;

    [Header("Camera")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private CameraManager _cameraManager;

    [Header("Audio")]
    [SerializeField] private PlayerAudioManager _playerAudioManager;

    private PlayerStance _playerStance;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private CapsuleCollider _collider;
    private PlayerStance _previousStance;

    private bool _isGrounded;
    private float _speed;
    private float _rotationSmoothVelocity;
    private bool _isPunching;
    private int _combo = 0;
    private bool _canGlide;
    private bool _isTPS;

    private Coroutine _isFalling;
    private Coroutine _resetCombo;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _speed = _walkSpeed;
        _playerStance = PlayerStance.Stand;
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider>();

        HideAndLockCursor();
    }

    private void Start()
    {
        _input.OnMoveInput += Move;
        _input.OnSprintInput += Sprint;
        _input.OnJumpInput += Jump;
        _input.OnClimbInput += StartClimb;
        _input.OnCancelClimb += CancelClimb;
        _input.OnCrouchInput += Crouch;
        _input.OnGlideInput += StartGlide;
        _input.OnCancelGlide += CancelGlide;
        _input.OnPunchInput += Punch;

        _input.OnChangePOV += ChangePerspective;

        _isTPS = true;
        _animator.SetBool("isTPS", _isTPS);
    }

    private void Update()
    {
        CheckIsGrounded();
        CheckStep();
        Glide();
        Debug.Log("Can Glide: " +  _canGlide);
    }

    private void Move(Vector2 axisDirection)
    {
        Vector3 movementDirection = Vector3.zero;
        bool isPlayerStanding = _playerStance == PlayerStance.Stand;
        bool isPlayerClimbing = _playerStance == PlayerStance.Climb;
        bool isPlayerCrouch = _playerStance == PlayerStance.Crounch;
        bool isPlayerGliding = _playerStance == PlayerStance.Glide;

        if ((isPlayerStanding || isPlayerCrouch) && !_isPunching)
        {
            switch (_cameraManager.CameraState)
            {
                case CameraState.ThirdPerson:

                    if (axisDirection.magnitude >= 0.1)
                    {
                        float rotationAngle = Mathf.Atan2(axisDirection.x, axisDirection.y) * Mathf.Rad2Deg + _cameraTransform.eulerAngles.y;
                        float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, rotationAngle, ref _rotationSmoothVelocity, _rotationSmoothTime);
                        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

                        movementDirection = Quaternion.Euler(0f, rotationAngle, 0f) * Vector3.forward;

                        //add some sloper checker
                        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _detectorRadius * 2, _detectorLayer))
                        {
                            movementDirection = Vector3.ProjectOnPlane(movementDirection, hit.normal);
                        }

                        _rigidbody.AddForce(movementDirection * Time.deltaTime * _speed);
                    }

                    break;

                case CameraState.FirstPerson:

                    transform.rotation = Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f);
                    Vector3 verticalDirection = axisDirection.y * transform.forward;
                    Vector3 horizontalDirection = axisDirection.x * transform.right;
                    movementDirection = verticalDirection + horizontalDirection;

                    _rigidbody.AddForce(movementDirection * Time.deltaTime * _speed);

                    break;

                default:
                    break;
            }

            Vector3 velocity = new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
            _animator.SetFloat("Velocity", velocity.magnitude * axisDirection.magnitude);
            _animator.SetFloat("VelocityX", velocity.magnitude * axisDirection.x);
            _animator.SetFloat("VelocityZ", velocity.magnitude * axisDirection.y);

            Debug.Log("Velocity: " + velocity.magnitude);

        }
        else if (isPlayerClimbing)
        {
            Vector3 horizontal = axisDirection.x * transform.right;
            Vector3 vertical = axisDirection.y * transform.up;
            movementDirection = horizontal + vertical;
            _rigidbody.AddForce(movementDirection * Time.deltaTime * _climbSpeed);

            Vector3 velocity = new Vector3(_rigidbody.velocity.x, _rigidbody.velocity.y, 0);
            _animator.SetFloat("ClimbVelocityX", velocity.magnitude * axisDirection.x);
            _animator.SetFloat("ClimbVelocityY", velocity.magnitude * axisDirection.y);
        }
        else if (isPlayerGliding)
        {
            Vector3 rotationDegree = transform.rotation.eulerAngles;
            rotationDegree.x += _glideRotationSpeed.x * axisDirection.y * Time.deltaTime;
            rotationDegree.x = Mathf.Clamp(rotationDegree.x, _minGlideRotationX, _maxGlideRotationX);
            rotationDegree.z += _glideRotationSpeed.z * axisDirection.x * Time.deltaTime;
            rotationDegree.y += _glideRotationSpeed.y * axisDirection.x * Time.deltaTime;
            transform.rotation = Quaternion.Euler(rotationDegree);
        }


    }

    private void Sprint(bool isSprint)
    {
        if (_playerStance != PlayerStance.Crounch) // Supaya ketika crouch jalan nya tidak ikut cepat
        {
            if (isSprint)
            {
                if (_speed < _sprintSpeed)
                {
                    _speed = _speed + _walkSprintTransition * Time.deltaTime;
                }
            }
            else
            {
                if (_speed > _walkSpeed)
                {
                    _speed = _speed - _walkSprintTransition * Time.deltaTime;
                }
            }
        }

        Debug.Log("Speed: " + _speed);
    }

    private void Jump()
    {
        if (_isGrounded)
        {
            Vector3 jumpDirection = Vector3.up;
            _rigidbody.AddForce(jumpDirection * _jumpForce);

            _animator.SetTrigger("Jump");

            if (_canGlide == true)
            {
                StopCoroutine(_isFalling);
                _canGlide = false;
            }
            _isFalling = StartCoroutine(IsFalling());
        }
    }

    private void Crouch()
    {
        if (_playerStance == PlayerStance.Stand)
        {
            _playerStance = PlayerStance.Crounch;
            _animator.SetBool("isCrouch", true);
            _speed = _crouchSpeed;

            _collider.height = 1.3f;
            _collider.center = Vector3.up * 0.66f;
        }
        else if (_playerStance == PlayerStance.Crounch)
        {
            _playerStance = PlayerStance.Stand;
            _animator.SetBool("isCrouch", false);
            _speed = _walkSpeed;

            _collider.height = 1.8f;
            _collider.center = Vector3.up * 0.9f;
        }
    }

    private void StartClimb()
    {
        if (Physics.Raycast(_climbDetector.position, transform.forward, out RaycastHit hit, _climbCheckDistance, _climbableLayer))
        {
            bool isNotClimbing = _playerStance != PlayerStance.Climb;

            //aligment or rotation of player checker
            float alignment = Vector3.Dot(transform.forward, -hit.normal);

            Debug.Log("Alignment: " + alignment);

            bool isFacingWall = alignment > 0.98f;

            if (_isGrounded && isNotClimbing && isFacingWall)
            {
                _previousStance = _playerStance; // Menyimpan stance saat ini sebelum climbing

                Vector3 offset = (transform.forward * _climbOffset.z) + (Vector3.up * _climbOffset.y);
                transform.position = hit.point - offset;
                _playerStance = PlayerStance.Climb;
                _rigidbody.useGravity = false;

                _cameraManager.SetFPSClampedCamera(true, transform.rotation.eulerAngles);
                _cameraManager.SetTPSFieldOfView(70);

                _animator.SetBool("isClimbing", true);
                _collider.height = 1.8f;
                _collider.center = Vector3.up * 1.3f;
            }
            else
            {
                Debug.Log("Not facing the wall properly!");
            }
        }
    }

    private void CancelClimb()
    {
        if (_playerStance == PlayerStance.Climb)
        {
            _playerStance = _previousStance; // Mengembalikan stance dari sebelum climb
            _rigidbody.useGravity = true;
            transform.position -= transform.forward * 1f;

            _cameraManager.SetFPSClampedCamera(false, transform.rotation.eulerAngles);
            _cameraManager.SetTPSFieldOfView(50);

            _animator.SetBool("isClimbing", false);
         
            if (_playerStance == PlayerStance.Crounch) // Untuk mencegah error ketika player climb dari stance crounch
            {
                _collider.height = 1.3f;
                _collider.center = Vector3.up * 0.66f;
                _animator.SetBool("isCrouch", true);
            }
            else
            {
                _collider.height = 1.8f;
                _collider.center = Vector3.up * 0.9f;
                _animator.SetBool("isCrouch", false);
            }
        }
    }

    private void StartGlide()
    {
        if (_playerStance != PlayerStance.Glide && !_isGrounded && _canGlide) // _canGlide mencegah player dapat gliding ketika loncat
        {
            _playerStance = PlayerStance.Glide;
            _animator.SetBool("isGliding", true);

            _cameraManager.SetFPSClampedCamera(true, transform.rotation.eulerAngles);
            _playerAudioManager.PlayGlideSFX();
        }
    }

    private void CancelGlide()
    {
        if (_playerStance == PlayerStance.Glide)
        {
            _playerStance = PlayerStance.Stand;
            _animator.SetBool("isGliding", false);

            _cameraManager.SetFPSClampedCamera(false, transform.rotation.eulerAngles);
            _playerAudioManager.StopGlideSFX();
        }
    }

    private void Glide()
    {
        if (_playerStance == PlayerStance.Glide)
        {
            Vector3 playerRotation = transform.rotation.eulerAngles;
            float lift = playerRotation.x;

            Vector3 upForce = transform.up * (lift + _airDrag);
            Vector3 forwardForce = transform.forward * _glideSpeed;
            Vector3 totalForce = upForce + forwardForce;
            _rigidbody.AddForce(totalForce * Time.deltaTime);
        }
    }

    private void Punch()
    {
        if (!_isPunching && _playerStance == PlayerStance.Stand)
        {
            _isPunching = true;

            if (_combo < 3)
            {
                _combo += 1;
            }
            else
            {
                _combo = 1;
            }

            _animator.SetInteger("Combo", _combo);
            _animator.SetTrigger("Punch");
        }
    }

    private void EndPunch()
    {
        _isPunching = false;

        if (_resetCombo != null)
        {
            StopCoroutine(_resetCombo);
        }
        _resetCombo = StartCoroutine(ResetCombo());
    }

    private void Hit()
    {
        Collider[] hitObjects = Physics.OverlapSphere(_hitDetector.position, _hitDetectorRadius, _hitLayer);

        for (int i = 0; i < hitObjects.Length; i++)
        {
            if (hitObjects[i].gameObject != null)
            {
                Destroy(hitObjects[i].gameObject);
            }
        }
    }

    private void CheckIsGrounded()
    {
        _isGrounded = Physics.CheckSphere(_groundDetector.position, _detectorRadius, _detectorLayer);

        _animator.SetBool("isGrounded", _isGrounded);

        if (_isGrounded)
        {
            CancelGlide();
            _canGlide = false;
        }
    }

    private void CheckStep()
    {
        bool isHitLowerStep = Physics.Raycast(_groundDetector.position, transform.forward, _stepCheckerDistance);
        bool isHitUpperStep = Physics.Raycast(_groundDetector.position + _upperStepOffset, transform.forward, _stepCheckerDistance);

        if (isHitLowerStep && !isHitUpperStep)
        {
            _rigidbody.AddForce(0, _stepForce, 0);
        }
    }

    private void HideAndLockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ChangePerspective()
    {
        _isTPS = _cameraManager.CameraState == CameraState.ThirdPerson;

        _animator.SetBool("isTPS", _isTPS); // Memakai Boolean untuk mencegah bug di animator
        Debug.Log("isTPS: " + _isTPS);
    }


    private void OnDestroy()
    {
        _input.OnMoveInput -= Move;
        _input.OnSprintInput -= Sprint;
        _input.OnJumpInput -= Jump;
        _input.OnClimbInput -= StartClimb;
        _input.OnCancelClimb -= CancelClimb;
        _input.OnCrouchInput -= Crouch;
        _input.OnGlideInput -= StartGlide;
        _input.OnCancelGlide -= CancelGlide;
        _input.OnPunchInput -= Punch;
        _input.OnChangePOV -= ChangePerspective;
    }

    private IEnumerator ResetCombo()
    {
        yield return new WaitForSeconds(_resetComboInterval);
        _combo = 0;
    }

    private IEnumerator IsFalling()
    {
        yield return new WaitForSeconds(1);
        _canGlide = true;
    }
}