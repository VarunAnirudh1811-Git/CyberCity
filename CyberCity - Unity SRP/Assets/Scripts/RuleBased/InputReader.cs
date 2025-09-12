using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputReader : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    private InputSystem_Actions inputActions;
    public event Action<Vector2> MoveEvent;
    public Vector2 MoveInput { get; private set; }

    private void OnEnable()
    {
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.SetCallbacks(this);
        }
        inputActions.Player.Enable();
    }
    private void OnDisable()
    {
        inputActions.Player.Disable();
    }
    public void OnMove(InputAction.CallbackContext context)
    {
        if (context.performed || context.canceled)
            MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }   

    void Start()
    {
        
    }
   
    void Update()
    {
        
    }
    public void OnAttack(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnJump(InputAction.CallbackContext context) { }
    public void OnLook(InputAction.CallbackContext context) { }
    public void OnNext(InputAction.CallbackContext context) { }
    public void OnPrevious(InputAction.CallbackContext context) { }
    public void OnSprint(InputAction.CallbackContext context) { }
}
