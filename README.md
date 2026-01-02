# Godot Finite State Machine (FSM)

A powerful, flexible, and feature-rich finite state machine implementation for Godot using C#. This state machine provides advanced functionality including state history, cooldowns, event-driven transitions, guards, and more.

## Features

- **Type-Safe States**: Uses C# enums for compile-time type safety
- **Flexible Transitions**: Support for conditional, event-based, and global transitions
- **State History**: Navigate backward through previous states
- **Cooldown System**: Prevent rapid state/transition cycling
- **Guard Conditions**: Pre-validate transitions before evaluation
- **Priority System**: Control transition evaluation order
- **State Timeouts**: Automatic transitions after duration
- **Event System**: Decouple state logic with events
- **Process Modes**: Support for both Idle and Fixed process modes
- **State Locking**: Prevent unwanted state changes
- **Data Storage**: Attach metadata to states and pass data between transitions
- **State Tags**: Group and query states by tags

## Installation

1. Copy all `.cs` files into your Godot project
2. Ensure they're in the `Godot.FSM` namespace
3. The files you need are:
   - `StateMachine.cs`
   - `State.cs`
   - `Transition.cs`
   - `Cooldown.cs`
   - `StateHistory.cs`

## Quick Start

### Basic Setup

```csharp
using Godot.FSM;

// Define your state enum
public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Jumping
}

// Create the state machine
var fsm = new StateMachine<PlayerState>();

// Add states
fsm.AddState(PlayerState.Idle)
    .OnEnter(() => GD.Print("Entered Idle"))
    .OnUpdate(delta => GD.Print($"Idling... {delta}"))
    .OnExit(() => GD.Print("Left Idle"));

fsm.AddState(PlayerState.Walking);
fsm.AddState(PlayerState.Running);
fsm.AddState(PlayerState.Jumping);

// Add transitions with conditions
fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
    .SetCondition(fsm => Input.IsActionPressed("move"));

fsm.AddTransition(PlayerState.Walking, PlayerState.Idle)
    .SetCondition(fsm => !Input.IsActionPressed("move"));

// Initialize and start
fsm.SetInitialId(PlayerState.Idle);
fsm.Start();
```

### Update Loop

```csharp
public override void _Process(double delta)
{
    fsm.Process(FSMProcessMode.Idle, (float)delta);
    fsm.UpdateCooldownTimers((float)delta);
}

public override void _PhysicsProcess(double delta)
{
    fsm.Process(FSMProcessMode.Fixed, (float)delta);
}
```

## Core Concepts

### States

States represent distinct conditions or behaviors in your system. Each state can have:

- **Enter callback**: Called when entering the state
- **Update callback**: Called every frame while in the state
- **Exit callback**: Called when leaving the state
- **Minimum time**: Prevent transitions until time elapsed
- **Timeout**: Automatically transition after duration
- **Process mode**: Choose between Idle or Fixed processing
- **Lock mode**: Prevent state changes
- **Tags**: Categorize states
- **Data**: Attach custom metadata
- **Cooldown**: Prevent re-entering too quickly

```csharp
fsm.AddState(PlayerState.Jumping)
    .OnEnter(() => velocity.Y = jumpForce)
    .OnUpdate(delta => ApplyGravity(delta))
    .OnExit(() => ResetJumpVariables())
    .SetMinTime(0.2f) // Must be in state for 0.2s before transitioning
    .SetTimeout(1.5f) // Auto-transition after 1.5s
    .SetTimeoutId(PlayerState.Idle) // Where to go on timeout
    .SetProcessMode(FSMProcessMode.Fixed)
    .SetCooldown(0.5f) // Can't re-enter for 0.5s after leaving
    .AddTags("airborne", "movement");
```

### Transitions

Transitions define how to move between states. They support:

- **Conditions**: Logic to trigger the transition
- **Guards**: Pre-checks before evaluation
- **Events**: Trigger on named events
- **Priority**: Control evaluation order
- **Minimum time overrides**: Per-transition timing
- **Instant transitions**: Skip minimum time requirements
- **Cooldowns**: Prevent rapid re-triggering
- **Callbacks**: Execute code when triggered

```csharp
// Conditional transition
fsm.AddTransition(PlayerState.Walking, PlayerState.Running)
    .SetCondition(fsm => Input.IsActionPressed("sprint"))
    .SetPriority(10);

// Guard prevents transition if player can't run
fsm.AddTransition(PlayerState.Idle, PlayerState.Running)
    .SetGuard(fsm => playerHasStamina)
    .SetCondition(fsm => Input.IsActionPressed("sprint"));

// Event-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Jumping)
    .OnEvent("jump_pressed")
    .SetCooldown(0.5f);

// Callback when triggered
fsm.AddTransition(PlayerState.Walking, PlayerState.Idle)
    .SetCondition(fsm => velocity.Length() < 0.1f)
    .OnTrigger(() => PlayStopAnimation());
```

### Guard vs Condition

Understanding the difference between guards and conditions is crucial:

**Guard**: Evaluated FIRST, before time requirements
- Use for pre-validation (is feature unlocked? is player alive?)
- Return `false` to reject the transition entirely
- Checked in both regular and event-based transitions
- Example: `SetGuard(fsm => player.IsAlive())`

**Condition**: Evaluated AFTER guard and time requirements
- Use for the actual transition trigger logic
- Return `true` to trigger the transition
- Only used in regular transitions (not events)
- Example: `SetCondition(fsm => player.Health < 20)`

```csharp
// Guard checks if ability is unlocked, condition checks if should trigger
fsm.AddTransition(PlayerState.Idle, PlayerState.SpecialAbility)
    .SetGuard(fsm => playerHasAbilityUnlocked) // Pre-check
    .SetCondition(fsm => Input.IsActionJustPressed("special")); // Trigger
```

## Advanced Features

### State History and Navigation

Track and navigate through previous states:

```csharp
// Enable history (enabled by default)
fsm.SetHistoryActive(true);

// Go back one state
if (fsm.CanGoBack())
    fsm.GoBack();

// Go back multiple states
fsm.GoBack(3);

// Go back to specific state
fsm.GoBackToState(PlayerState.Idle);

// Check how far back a state is
int stepsBack = fsm.FindInHistory(PlayerState.Walking);

// Peek at previous state without transitioning
PlayerState previousState = fsm.PeekBackState();

// Get full history
var history = fsm.StateHistory.GetHistory();
foreach (var entry in history)
{
    GD.Print($"State: {entry.StateId}, Time: {entry.TimeSpent}s");
}

// Set history capacity
fsm.StateHistory.SetCapacity(50);
```

### Event System

Decouple state logic with events:

```csharp
// Set up event listener
fsm.OnEvent("player_died", () => {
    GD.Print("Player died!");
    ResetGame();
});

// Create event-based transition
fsm.AddTransition(PlayerState.Walking, PlayerState.Dead)
    .OnEvent("player_died");

// Trigger event from anywhere
fsm.SendEvent("player_died");

// Remove listener
fsm.RemoveEventListener("player_died", callbackReference);
```

### Global Transitions

Transitions that work from any state:

```csharp
// Can transition to Dead from any state
fsm.AddGlobalTransition(PlayerState.Dead)
    .SetCondition(fsm => player.Health <= 0);

// Can pause from any state
fsm.AddGlobalTransition(PlayerState.Paused)
    .OnEvent("pause_pressed");

// Check if global transition exists
bool hasPauseTransition = fsm.HasGlobalTransition(PlayerState.Paused);
```

### State Locking

Prevent unwanted state changes:

```csharp
// Full lock: prevents all transitions and exit
fsm.AddState(PlayerState.Cutscene)
    .Lock(FSMLockMode.Full);

// Transition lock: prevents transitions but allows exit
fsm.AddState(PlayerState.Attacking)
    .Lock(FSMLockMode.Transition);

// Unlock later
var state = fsm.GetState(PlayerState.Attacking);
state.Unlock();

// Check lock status
if (state.IsLocked()) { }
if (state.IsFullyLocked()) { }
if (state.TransitionBlocked()) { }
```

### Data Storage

Store and retrieve data:

```csharp
// Global data (available everywhere)
fsm.SetData("player_score", 1000);
if (fsm.TryGetData<int>("player_score", out int score))
{
    GD.Print($"Score: {score}");
}

// Per-state data
fsm.GetState(PlayerState.Combat)
    .SetData("combo_count", 0)
    .SetData("damage_multiplier", 1.5f);

if (state.TryGetData<int>("combo_count", out int combo))
{
    // Use combo count
}

// Per-transition data (passed during transition)
fsm.TryChangeState(PlayerState.Dialogue, data: dialogueId);

// Retrieve in next state's Enter callback
var dialogueId = fsm.GetPerTransitionData<string>();
```

### Cooldown System

Prevent rapid state/transition cycling:

```csharp
// State cooldown - can't re-enter for 2 seconds after leaving
fsm.AddState(PlayerState.Dash)
    .SetCooldown(2.0f);

// Transition cooldown - can only trigger once per second
fsm.AddTransition(PlayerState.Idle, PlayerState.Attack)
    .SetCooldown(1.0f);

// Check cooldown status
if (fsm.IsStateOnCooldown(PlayerState.Dash))
{
    GD.Print("Dash is on cooldown");
}

if (fsm.IsTransitionOnCooldown(PlayerState.Idle, PlayerState.Attack))
{
    GD.Print("Can't attack yet");
}

// Reset cooldowns
fsm.ResetStateCooldown(PlayerState.Dash);
fsm.ResetTransitionCooldown(PlayerState.Idle, PlayerState.Attack);
fsm.ResetAllCooldowns();

// Get cooldown count
int activeCooldowns = fsm.GetActiveCooldownCount();
```

### State Tags

Organize and query states:

```csharp
// Add tags to states
fsm.AddState(PlayerState.Walking).AddTags("grounded", "movement");
fsm.AddState(PlayerState.Running).AddTags("grounded", "movement");
fsm.AddState(PlayerState.Jumping).AddTags("airborne", "movement");

// Check current state tags
if (fsm.IsInStateWithTag("grounded"))
{
    // Player is on ground
}

// Get all states with tag
var groundedStates = fsm.GetStatesWithTag("grounded");

// Get first state with tag
var combatState = fsm.GetStateByTag("combat");
```

### Manual State Changes

Sometimes you need direct control:

```csharp
// Try to change state with optional condition
bool success = fsm.TryChangeState(
    PlayerState.Running,
    condition: () => player.HasStamina(),
    data: speedBoost
);

// Restart current state
fsm.RestartCurrentState(callEnter: true, callExit: false);

// Reset to initial state
fsm.Reset();
```

### Utility Methods

```csharp
// State queries
T currentId = fsm.GetCurrentId();
T previousId = fsm.GetPreviousId();
string stateName = fsm.GetCurrentStateName();
bool isInIdle = fsm.IsCurrentState(PlayerState.Idle);
bool wasWalking = fsm.IsPreviousState(PlayerState.Walking);

// Timing
float stateTime = fsm.GetStateTime();
float lastStateTime = fsm.GetLastStateTime();
float minTime = fsm.GetMinStateTime();
float remainingTimeout = fsm.GetRemainingTime();
float timeoutProgress = fsm.GetTimeoutProgress(); // 0-1

// Check if minimum time exceeded
if (fsm.MintimeExceeded())
{
    // Can transition now
}

// Pause/Resume
fsm.Pause();
fsm.Resume();
fsm.TogglePaused(true);
bool isActive = fsm.IsActive();

// Transition queries
bool hasTransition = fsm.HasTransition(PlayerState.Idle, PlayerState.Walking);
bool hasGlobal = fsm.HasGlobalTransition(PlayerState.Dead);
var available = fsm.GetAvailableTransitions();
```

## Complete Example: Character Controller

```csharp
using Godot;
using Godot.FSM;

public partial class CharacterController : CharacterBody2D
{
    public enum CharacterState
    {
        Idle,
        Walking,
        Running,
        Jumping,
        Falling,
        Attacking,
        Dead
    }

    private StateMachine<CharacterState> fsm;
    private float moveSpeed = 200f;
    private float runSpeed = 350f;
    private float jumpForce = -400f;
    private float gravity = 980f;

    public override void _Ready()
    {
        InitializeStateMachine();
    }

    private void InitializeStateMachine()
    {
        fsm = new StateMachine<CharacterState>();

        // Idle State
        fsm.AddState(CharacterState.Idle)
            .OnEnter(() => Velocity = Vector2.Zero)
            .OnUpdate(delta => ApplyGravity(delta))
            .SetMinTime(0.1f);

        // Walking State
        fsm.AddState(CharacterState.Walking)
            .OnUpdate(delta => {
                ApplyGravity(delta);
                Move(moveSpeed, delta);
            });

        // Running State
        fsm.AddState(CharacterState.Running)
            .OnEnter(() => GD.Print("Started running!"))
            .OnUpdate(delta => {
                ApplyGravity(delta);
                Move(runSpeed, delta);
            })
            .SetCooldown(1.0f); // Can't run again for 1s

        // Jumping State
        fsm.AddState(CharacterState.Jumping)
            .OnEnter(() => Velocity = new Vector2(Velocity.X, jumpForce))
            .OnUpdate(delta => {
                ApplyGravity(delta);
                Move(moveSpeed, delta);
            })
            .SetTimeout(0.5f)
            .SetTimeoutId(CharacterState.Falling)
            .AddTags("airborne");

        // Falling State
        fsm.AddState(CharacterState.Falling)
            .OnUpdate(delta => {
                ApplyGravity(delta);
                Move(moveSpeed, delta);
            })
            .AddTags("airborne");

        // Attacking State
        fsm.AddState(CharacterState.Attacking)
            .OnEnter(() => PlayAttackAnimation())
            .Lock(FSMLockMode.Transition) // Can't transition during attack
            .SetTimeout(0.5f)
            .SetTimeoutId(CharacterState.Idle);

        // Dead State
        fsm.AddState(CharacterState.Dead)
            .OnEnter(() => GD.Print("Player died!"))
            .Lock(FSMLockMode.Full);

        // Transitions
        fsm.AddTransition(CharacterState.Idle, CharacterState.Walking)
            .SetCondition(fsm => IsGrounded() && GetMoveInput() != 0);

        fsm.AddTransition(CharacterState.Walking, CharacterState.Running)
            .SetCondition(fsm => Input.IsActionPressed("sprint"));

        fsm.AddTransition(CharacterState.Running, CharacterState.Walking)
            .SetCondition(fsm => !Input.IsActionPressed("sprint"));

        fsm.AddTransition(CharacterState.Walking, CharacterState.Idle)
            .SetCondition(fsm => GetMoveInput() == 0);

        fsm.AddTransition(CharacterState.Running, CharacterState.Idle)
            .SetCondition(fsm => GetMoveInput() == 0);

        fsm.AddTransition(CharacterState.Idle, CharacterState.Jumping)
            .OnEvent("jump")
            .SetCooldown(0.2f);

        fsm.AddTransition(CharacterState.Walking, CharacterState.Jumping)
            .OnEvent("jump")
            .SetCooldown(0.2f);

        fsm.AddTransition(CharacterState.Falling, CharacterState.Idle)
            .SetCondition(fsm => IsGrounded());

        fsm.AddTransition(CharacterState.Idle, CharacterState.Attacking)
            .OnEvent("attack");

        // Global transition to Dead state
        fsm.AddGlobalTransition(CharacterState.Dead)
            .SetCondition(fsm => GetHealth() <= 0);

        // Start FSM
        fsm.SetInitialId(CharacterState.Idle);
        fsm.Start();
    }

    public override void _Process(double delta)
    {
        fsm.Process(FSMProcessMode.Idle, (float)delta);
        fsm.UpdateCooldownTimers((float)delta);
        
        HandleInput();
    }

    public override void _PhysicsProcess(double delta)
    {
        fsm.Process(FSMProcessMode.Fixed, (float)delta);
        MoveAndSlide();
    }

    private void HandleInput()
    {
        if (Input.IsActionJustPressed("jump"))
            fsm.SendEvent("jump");
        
        if (Input.IsActionJustPressed("attack"))
            fsm.SendEvent("attack");
    }

    private void ApplyGravity(float delta)
    {
        if (!IsGrounded())
            Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * delta);
    }

    private void Move(float speed, float delta)
    {
        float direction = GetMoveInput();
        Velocity = new Vector2(direction * speed, Velocity.Y);
    }

    private float GetMoveInput()
    {
        return Input.GetAxis("move_left", "move_right");
    }

    private bool IsGrounded()
    {
        return IsOnFloor();
    }

    private float GetHealth()
    {
        // Your health logic here
        return 100f;
    }

    private void PlayAttackAnimation()
    {
        // Animation logic
    }
}
```

## Best Practices

1. **Use Guards for Pre-conditions**: Check if a transition is even possible before evaluating conditions
2. **Set Minimum Times**: Prevent rapid state flickering with appropriate minimum times
3. **Use Cooldowns Wisely**: Prevent spam and add weight to actions
4. **Leverage Tags**: Group related states for easier queries
5. **Event-Based Transitions**: Use events to decouple input handling from state logic
6. **State Locking**: Protect critical states from interruption
7. **History for Undo**: Enable history when you need "go back" functionality
8. **Process Modes**: Use Fixed for physics-dependent states, Idle for visual updates
9. **Global Transitions**: Perfect for panic states (death, pause, etc.)
10. **Clean Up**: Call `Dispose()` when done with the state machine

## API Reference

### StateMachine<T>

**Events**
- `StateChanged(T from, T to)` - Fired when state changes
- `TransitionTriggered(T from, T to)` - Fired when transition triggers
- `TimeoutBlocked(T state)` - Fired when timeout is blocked
- `StateTimeout(T state)` - Fired when state times out

**Core Methods**
- `AddState(T id)` - Add a new state
- `RemoveState(T id)` - Remove a state
- `Start()` - Start the state machine
- `Reset()` - Reset to initial state
- `SetInitialId(T id)` - Set starting state
- `Process(FSMProcessMode mode, float delta)` - Update the FSM
- `UpdateCooldownTimers(float delta)` - Update all cooldowns

**Transitions**
- `AddTransition(T from, T to)` - Add transition
- `AddGlobalTransition(T to)` - Add global transition
- `AddResetTransition(T from)` - Add transition to initial state
- `AddSelfTransition(T id)` - Add self-loop transition
- `RemoveTransition(T from, T to)` - Remove transition
- `ClearTransitions()` - Clear all transitions

**Events**
- `SendEvent(string eventName)` - Trigger an event
- `OnEvent(string eventName, Action callback)` - Listen for event
- `RemoveEventListener(string eventName, Action callback)` - Stop listening

**History**
- `GoBack()` - Go back one state
- `GoBack(int steps)` - Go back multiple states
- `GoBackToState(T id)` - Go back to specific state
- `CanGoBack()` - Check if can go back
- `PeekBackState()` - Look at previous state
- `FindInHistory(T stateId)` - Find state in history

**Queries**
- `GetCurrentId()` - Get current state ID
- `GetPreviousId()` - Get previous state ID
- `GetState(T id)` - Get state object
- `IsCurrentState(T id)` - Check current state
- `GetStateTime()` - Time in current state

### State<T>

**Configuration**
- `OnEnter(Action enter)` - Set enter callback
- `OnUpdate(Action<float> update)` - Set update callback
- `OnExit(Action exit)` - Set exit callback
- `SetMinTime(float duration)` - Set minimum time
- `SetTimeout(float duration)` - Set timeout duration
- `SetTimeoutId(T id)` - Set timeout target
- `Lock(FSMLockMode mode)` - Lock state
- `SetCooldown(float duration)` - Set cooldown
- `AddTags(params string[] tags)` - Add tags
- `SetData(string id, object value)` - Store data

### Transition<T>

**Configuration**
- `SetCondition(Predicate<StateMachine<T>> condition)` - Set trigger condition
- `SetGuard(Predicate<StateMachine<T>> guard)` - Set pre-check guard
- `OnEvent(string eventName)` - Trigger on event
- `OnTrigger(Action method)` - Callback when triggered
- `SetPriority(int priority)` - Set evaluation priority
- `SetMinTime(float value)` - Override state minimum time
- `ForceInstant()` - Skip minimum time check
- `SetCooldown(float duration)` - Set cooldown

## License

This state machine is provided as-is for use in your Godot projects.
