# Godot FSM Library

A powerful and flexible Finite State Machine library for Godot with C#. This library provides a robust foundation for managing complex state-based behavior in your games.

## Features

- **Type-safe state management** using C# enums
- **Flexible transitions** with conditions, guards, and priorities
- **Event-driven architecture** for triggering transitions
- **State and transition cooldowns** to prevent rapid state changes
- **State history system** with navigation support (go back, peek history)
- **Timeout support** for automatic state transitions
- **Global transitions** that work from any state
- **State locking** to prevent unwanted transitions
- **Custom data storage** per state and globally
- **State tagging** for grouping and querying states
- **Dual process modes** (Idle/Fixed) for frame-rate independent updates

## Quick Start

### 1. Define Your States

```csharp
public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Jumping,
    Attacking
}
```

### 2. Create and Initialize the State Machine

```csharp
// Create the state machine
var fsm = new StateMachine<PlayerState>();

// Add states with callbacks
fsm.AddState(PlayerState.Idle)
    .OnEnter(() => GD.Print("Entered Idle"))
    .OnUpdate(delta => { /* Update logic */ })
    .OnExit(() => GD.Print("Left Idle"));

fsm.AddState(PlayerState.Walking)
    .OnEnter(() => PlayAnimation("walk"))
    .MinDuration(0.2f); // Minimum time before transitioning

fsm.AddState(PlayerState.Jumping)
    .OnEnter(() => ApplyJumpForce())
    .TimeoutAfter(1.0f) // Auto-transition after 1 second
    .SetTimeoutId(PlayerState.Idle);

// Start the state machine
fsm.Start();
```

### 3. Add Transitions

```csharp
// Basic transition with condition
fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
    .SetCondition(fsm => Input.IsActionPressed("move"));

// Transition with guard (pre-condition check)
fsm.AddTransition(PlayerState.Walking, PlayerState.Running)
    .SetGuard(fsm => !isExhausted) // Check first
    .SetCondition(fsm => Input.IsActionPressed("sprint")); // Then check condition

// Event-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Attacking)
    .OnEvent("attack_pressed");

// Priority-based transition (higher priority checked first)
fsm.AddTransition(PlayerState.Walking, PlayerState.Jumping)
    .SetCondition(fsm => Input.IsActionJustPressed("jump"))
    .SetPriority(10);
```

### 4. Update the State Machine

```csharp
public override void _Process(double delta)
{
    fsm.UpdateIdle((float)delta);
}

public override void _PhysicsProcess(double delta)
{
    fsm.UpdateFixed((float)delta);
}
```

## Core Concepts

### States

States represent distinct behaviors or modes in your game. Each state can have:

- **Enter callback**: Called when entering the state
- **Update callback**: Called every frame while in the state
- **Exit callback**: Called when leaving the state
- **Minimum duration**: Prevents transitions until time elapsed
- **Timeout**: Automatically transitions after duration
- **Process mode**: Update in Idle or Fixed process
- **Lock mode**: Prevents transitions (Full, Transition, or None)
- **Cooldown**: Prevents re-entering the state for a duration
- **Tags**: Labels for grouping and querying
- **Custom data**: Store state-specific data

```csharp
fsm.AddState(PlayerState.Attacking)
    .OnEnter(() => StartAttackAnimation())
    .OnUpdate(delta => UpdateAttackHitbox())
    .OnExit(() => ResetAttackState())
    .MinDuration(0.3f)
    .SetProcessMode(FSMProcessMode.Idle)
    .Lock(FSMLockMode.Full) // Can't leave until timeout/unlock
    .SetCooldown(2.0f) // Can't attack again for 2 seconds
    .AddTags("combat", "action")
    .SetData("damage", 25);
```

### Transitions

Transitions define how states connect. A transition can have:

- **Condition**: Main logic to trigger the transition
- **Guard**: Pre-condition check (evaluated before condition)
- **Event**: Trigger via named event
- **Priority**: Higher priority transitions are checked first
- **MinTime override**: Custom minimum time for this transition
- **ForceInstant**: Bypass minimum time requirements
- **Cooldown**: Prevent rapid re-triggering
- **Callback**: Execute code when transition fires

```csharp
fsm.AddTransition(PlayerState.Idle, PlayerState.Attacking)
    .SetGuard(fsm => hasWeapon && !isStunned) // Check prerequisites
    .SetCondition(fsm => Input.IsActionJustPressed("attack")) // Main trigger
    .OnTrigger(() => PlayAttackSound())
    .SetPriority(5)
    .SetCooldown(0.5f); // Can only trigger once per 0.5 seconds
```

### Guard vs Condition

- **Guard**: Checked **first**, used for early rejection of invalid transitions
  - Example: "Is player alive?", "Is feature unlocked?", "Has required item?"
  - Return `false` to completely skip this transition
  
- **Condition**: Checked **after** guard and time requirements
  - Example: "Is health low?", "Is enemy in range?", "Button pressed?"
  - Return `true` to trigger the transition

Both are checked in regular transitions. Event-based transitions only check guards.

### Global Transitions

Global transitions work from any state, useful for universal interrupts:

```csharp
// Can transition to Death from any state
fsm.AddGlobalTransition(PlayerState.Death)
    .SetCondition(fsm => health <= 0);

// Pause from any state
fsm.AddGlobalTransition(PlayerState.Paused)
    .OnEvent("pause_pressed");
```

### Events

Events provide an alternative way to trigger transitions:

```csharp
// Set up event listener
fsm.OnEvent("enemy_spotted", () => GD.Print("Enemy detected!"));

// Create event-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Combat)
    .OnEvent("enemy_spotted");

// Trigger the event
fsm.TriggerEvent("enemy_spotted");
```

### State History

Navigate through previous states:

```csharp
// Go back one state
fsm.GoBack();

// Go back multiple states
fsm.GoBack(3);

// Go back to a specific state
fsm.GoBackToState(PlayerState.Idle);

// Peek at previous states without changing
var previousState = fsm.PeekBackState();
var twoStatesAgo = fsm.PeekBackState(2);

// Find how many steps back a state is
int steps = fsm.FindInHistory(PlayerState.Walking);

// Get full history
var history = fsm.StateHistory.GetHistory();
```

### Cooldowns

Prevent rapid state changes and transition spam:

```csharp
// State cooldown - can't re-enter for 2 seconds
fsm.AddState(PlayerState.Dashing)
    .SetCooldown(2.0f);

// Transition cooldown - can't re-trigger for 1 second
fsm.AddTransition(PlayerState.Idle, PlayerState.Dashing)
    .SetCooldown(1.0f);

// Check cooldown status
if (fsm.IsStateOnCooldown(PlayerState.Dashing))
    GD.Print("Dash not ready yet");

// Reset cooldowns
fsm.ResetStateCooldown(PlayerState.Dashing);
fsm.ResetAllCooldowns();
```

## Advanced Features

### Data Storage

Store data at state or machine level:

```csharp
// State data
fsm.GetState(PlayerState.Attacking)
    .SetData("combo_count", 0);

if (state.TryGetData<int>("combo_count", out var count))
    GD.Print($"Combo: {count}");

// Global data
fsm.SetData("player_score", 1000);
if (fsm.TryGetData<int>("player_score", out var score))
    GD.Print($"Score: {score}");

// Per-transition data (temporary)
fsm.TryTransitionTo(PlayerState.Damaged, data: damageInfo);
var damage = fsm.GetPerTransitionData<DamageInfo>();
```

### State Locking

Prevent transitions when needed:

```csharp
// Full lock - can't transition at all
fsm.AddState(PlayerState.Cutscene)
    .Lock(FSMLockMode.Full);

// Transition lock - can timeout but can't manually transition
fsm.AddState(PlayerState.Stunned)
    .Lock(FSMLockMode.Transition)
    .TimeoutAfter(1.5f)
    .SetTimeoutId(PlayerState.Idle);

// Unlock later
fsm.GetState(PlayerState.Cutscene).Unlock();
```

### Process Modes

Control when states update:

```csharp
// Update during _Process (Idle)
fsm.AddState(PlayerState.Walking)
    .SetProcessMode(FSMProcessMode.Idle);

// Update during _PhysicsProcess (Fixed)
fsm.AddState(PlayerState.Jumping)
    .SetProcessMode(FSMProcessMode.Fixed);
```

### State Queries

Find and check states:

```csharp
// Current state checks
if (fsm.IsCurrentState(PlayerState.Attacking))
    GD.Print("Currently attacking");

// Tag-based queries
if (fsm.IsInStateWithTag("combat"))
    GD.Print("In combat state");

var combatStates = fsm.GetStatesWithTag("combat");

// State information
var currentId = fsm.GetCurrentId();
var previousId = fsm.GetPreviousId();
var stateTime = fsm.GetStateTime();
```

## Events and Callbacks

Subscribe to state machine events:

```csharp
// State changed
fsm.StateChanged += (from, to) => 
    GD.Print($"Transitioned: {from} -> {to}");

// Transition triggered
fsm.TransitionTriggered += (from, to) => 
    GD.Print($"Transition: {from} -> {to}");

// Timeout occurred
fsm.StateTimeout += (state) => 
    GD.Print($"State {state} timed out");

// Timeout blocked by lock
fsm.TimeoutBlocked += (state) => 
    GD.Print($"Timeout blocked for {state}");
```

## Best Practices

1. **Use guards for prerequisites**: Check conditions that make a transition invalid (has weapon, is alive, etc.)
2. **Use conditions for triggers**: Check the actual trigger logic (button pressed, enemy in range, etc.)
3. **Set minimum durations**: Prevent state flickering by requiring minimum time in states
4. **Use cooldowns wisely**: Prevent spam and balance gameplay
5. **Tag your states**: Group related states for easier queries
6. **Lock critical states**: Prevent interruption during important animations or cutscenes
7. **Use events for complex triggers**: Decouple state transitions from input handling
8. **Leverage state history**: Implement undo/retry mechanics naturally

## Example: Complete Player Controller

```csharp
public enum PlayerState { Idle, Walk, Run, Jump, Fall, Attack }

public class Player : Node2D
{
    private StateMachine<PlayerState> fsm;
    
    public override void _Ready()
    {
        fsm = new StateMachine<PlayerState>();
        
        // Setup states
        fsm.AddState(PlayerState.Idle)
            .OnEnter(() => PlayAnimation("idle"));
            
        fsm.AddState(PlayerState.Walk)
            .OnEnter(() => PlayAnimation("walk"))
            .OnUpdate(delta => Move(walkSpeed * delta));
            
        fsm.AddState(PlayerState.Run)
            .OnEnter(() => PlayAnimation("run"))
            .OnUpdate(delta => Move(runSpeed * delta));
            
        fsm.AddState(PlayerState.Jump)
            .OnEnter(() => ApplyJumpForce())
            .SetProcessMode(FSMProcessMode.Fixed);
            
        fsm.AddState(PlayerState.Attack)
            .OnEnter(() => PlayAnimation("attack"))
            .MinDuration(0.4f)
            .SetCooldown(0.5f);
        
        // Setup transitions
        fsm.AddTransition(PlayerState.Idle, PlayerState.Walk)
            .SetCondition(fsm => IsMoving() && !IsRunPressed());
            
        fsm.AddTransition(PlayerState.Walk, PlayerState.Run)
            .SetCondition(fsm => IsRunPressed());
            
        fsm.AddTransition(PlayerState.Run, PlayerState.Walk)
            .SetCondition(fsm => !IsRunPressed());
            
        var toIdle = new[] { PlayerState.Walk, PlayerState.Run };
        fsm.AddTransitions(toIdle, PlayerState.Idle, 
            fsm => !IsMoving());
            
        // Jump from walk or run
        fsm.AddTransition(PlayerState.Walk, PlayerState.Jump)
            .SetCondition(fsm => Input.IsActionJustPressed("jump"))
            .ForceInstant();
            
        fsm.AddTransition(PlayerState.Run, PlayerState.Jump)
            .SetCondition(fsm => Input.IsActionJustPressed("jump"))
            .ForceInstant();
        
        // Attack from idle/walk
        fsm.AddTransition(PlayerState.Idle, PlayerState.Attack)
            .OnEvent("attack");
            
        fsm.AddTransition(PlayerState.Walk, PlayerState.Attack)
            .OnEvent("attack");
        
        fsm.Start();
    }
    
    public override void _Process(double delta)
    {
        fsm.UpdateIdle((float)delta);
        
        if (Input.IsActionJustPressed("attack"))
            fsm.TriggerEvent("attack");
    }
    
    public override void _PhysicsProcess(double delta)
    {
        fsm.UpdateFixed((float)delta);
    }
}
```

## License

This library is provided as-is for use in your Godot projects.
