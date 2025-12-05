# State Machine Usage Guide

## Table of Contents
1. [Introduction](#introduction)
2. [Basic Setup](#basic-setup)
3. [Adding States](#adding-states)
4. [State Callbacks](#state-callbacks)
5. [Transitions](#transitions)
6. [Advanced Features](#advanced-features)
7. [Complete Examples](#complete-examples)

---

## Introduction

This is a generic Finite State Machine (FSM) implementation for Godot that supports:
- State management with enter/exit callbacks
- Conditional transitions with priorities
- Global transitions (from any state)
- Event-driven transitions
- State timeouts and minimum times
- Data passing between states
- State locking mechanisms
- Debug utilities

---

## Basic Setup

### 1. Define Your State Enum

```csharp
public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Jumping,
    Falling,
    Dead
}
```

### 2. Create and Initialize the State Machine

```csharp
public partial class Player : CharacterBody2D
{
    private StateMachine<PlayerState> stateMachine;

    public override void _Ready()
    {
        // Create the state machine
        stateMachine = new StateMachine<PlayerState>();
        
        // Add states
        stateMachine.AddState(PlayerState.Idle);
        stateMachine.AddState(PlayerState.Walking);
        stateMachine.AddState(PlayerState.Running);
        
        // Start the state machine (transitions to initial state)
        stateMachine.Start();
    }
}
```

### 3. Process the State Machine

```csharp
public override void _Process(double delta)
{
    // Process during idle frames
    stateMachine.Process(FSMProcessMode.Idle, delta);
}

public override void _PhysicsProcess(double delta)
{
    // Process during physics frames
    stateMachine.Process(FSMProcessMode.Physics, delta);
}
```

---

## Adding States

### Basic State Addition

```csharp
var idleState = stateMachine.AddState(PlayerState.Idle);
var walkState = stateMachine.AddState(PlayerState.Walking);
var runState = stateMachine.AddState(PlayerState.Running);
```

### Configure State Properties

```csharp
var jumpState = stateMachine.AddState(PlayerState.Jumping);

// Set which frame type this state processes on
jumpState.SetProcessMode(FSMProcessMode.Physics);

// Set minimum time before transitions can occur (in seconds)
jumpState.SetMinTime(0.2f);

// Set timeout - automatically transition after this duration
jumpState.SetTimeout(1.0f);

// Set where to go when timeout occurs
jumpState.SetRestart(PlayerState.Falling);

// Add tags for querying
jumpState.AddTag("airborne");
jumpState.AddTag("mobile");
```

---

## State Callbacks

### Enter and Exit Callbacks

```csharp
var walkState = stateMachine.AddState(PlayerState.Walking);

walkState.SetEnter(() => 
{
    GD.Print("Started walking");
    animationPlayer.Play("walk");
});

walkState.SetExit(() => 
{
    GD.Print("Stopped walking");
});
```

### Update Callback

```csharp
walkState.SetUpdate((delta) => 
{
    // Called every frame while in this state
    var direction = Input.GetAxis("move_left", "move_right");
    Velocity = new Vector2(direction * speed, Velocity.Y);
    MoveAndSlide();
});
```

### Timeout Callback

```csharp
var stunState = stateMachine.AddState(PlayerState.Stunned);
stunState.SetTimeout(2.0f);
stunState.SetRestart(PlayerState.Idle);

stunState.SetCallback(() => 
{
    GD.Print("Stun duration ended!");
});
```

---

## Transitions

### Basic Transitions

```csharp
// Add transition from Idle to Walking
stateMachine.AddTransition(PlayerState.Idle, PlayerState.Walking);

// Add transition from Walking back to Idle
stateMachine.AddTransition(PlayerState.Walking, PlayerState.Idle);
```

### Conditional Transitions

```csharp
// Transition with a condition
stateMachine.AddTransition(PlayerState.Idle, PlayerState.Walking)
    .SetCondition(fsm => 
    {
        var direction = Input.GetAxis("move_left", "move_right");
        return Mathf.Abs(direction) > 0.1f;
    });

// Transition from Walking to Running when shift is pressed
stateMachine.AddTransition(PlayerState.Walking, PlayerState.Running)
    .SetCondition(fsm => Input.IsActionPressed("sprint"));
```

### Transition Priorities

Lower numbers = higher priority (checked first)

```csharp
// High priority transition (checked first)
stateMachine.AddTransition(PlayerState.Walking, PlayerState.Jumping)
    .SetPriority(1)
    .SetCondition(fsm => Input.IsActionJustPressed("jump"));

// Lower priority transition (checked after)
stateMachine.AddTransition(PlayerState.Walking, PlayerState.Idle)
    .SetPriority(10)
    .SetCondition(fsm => 
    {
        var direction = Input.GetAxis("move_left", "move_right");
        return Mathf.Abs(direction) < 0.1f;
    });
```

### Global Transitions

Global transitions can trigger from ANY state:

```csharp
// Player can die from any state
stateMachine.AddGlobalTransition(PlayerState.Dead)
    .SetCondition(fsm => health <= 0);

// Pause from any state
stateMachine.AddGlobalTransition(PlayerState.Paused)
    .SetCondition(fsm => Input.IsActionJustPressed("pause"));
```

### Multiple Source Transitions

```csharp
// Transition to Falling from multiple states
var airborneStates = new[] { PlayerState.Jumping, PlayerState.WallSliding };
stateMachine.AddTransitions(
    airborneStates, 
    PlayerState.Falling,
    fsm => Velocity.Y > 0 && !IsOnFloor()
);
```

### Guards (Pre-conditions)

Guards are checked BEFORE conditions and can block transitions:

```csharp
stateMachine.AddTransition(PlayerState.Idle, PlayerState.Attacking)
    .SetGuard(fsm => !fsm.IsInStateWithTag("locked")) // Check if not locked
    .SetCondition(fsm => Input.IsActionJustPressed("attack"));
```

### Transition Callbacks

```csharp
stateMachine.AddTransition(PlayerState.Idle, PlayerState.Jumping)
    .SetCondition(fsm => Input.IsActionJustPressed("jump"))
    .SetTriggered(() => 
    {
        GD.Print("Jump transition triggered!");
        PlaySound("jump");
    });
```

---

## Advanced Features

### Event-Based Transitions

```csharp
// Set up event listener
stateMachine.OnEvent("player_landed", () => 
{
    GD.Print("Player landed!");
});

// Create event-based transition
stateMachine.AddTransition(PlayerState.Falling, PlayerState.Idle)
    .SetEventName("player_landed");

// Trigger the event elsewhere in your code
public override void _PhysicsProcess(double delta)
{
    bool wasInAir = !IsOnFloor();
    MoveAndSlide();
    
    if (wasInAir && IsOnFloor())
    {
        stateMachine.SendEvent("player_landed");
    }
}
```

### State Locking

```csharp
var attackState = stateMachine.AddState(PlayerState.Attacking);

// Prevent ALL transitions and exit callbacks
attackState.SetLockMode(FSMLockMode.Full);

// Only prevent transitions (exit callbacks still work)
attackState.SetLockMode(FSMLockMode.Transition);

// Unlock when animation completes
attackState.SetEnter(() => 
{
    attackState.SetLockMode(FSMLockMode.Full);
    animationPlayer.Play("attack");
});

animationPlayer.AnimationFinished += (animName) => 
{
    if (animName == "attack")
        attackState.SetLockMode(FSMLockMode.None);
};
```

### Data Passing

#### Global Data
```csharp
// Store data accessible from any state
stateMachine.SetData("player_health", 100);
stateMachine.SetData("combo_count", 0);

// Retrieve data
if (stateMachine.TryGetData<int>("combo_count", out int combo))
{
    GD.Print($"Current combo: {combo}");
}
```

#### Per-Transition Data
```csharp
// Pass data when changing states
stateMachine.TryChangeState(PlayerState.Hit, data: new HitData 
{ 
    Damage = 25, 
    Knockback = new Vector2(100, -50) 
});

// Retrieve in the Enter callback
var hitState = stateMachine.AddState(PlayerState.Hit);
hitState.SetEnter(() => 
{
    if (stateMachine.TryGetPerTransitionData<HitData>(out var hitData))
    {
        health -= hitData.Damage;
        Velocity = hitData.Knockback;
    }
});
```

### State Events

```csharp
// Subscribe to state machine events
stateMachine.StateChanged += (from, to) => 
{
    GD.Print($"State changed: {from} -> {to}");
};

stateMachine.StateTimeout += (state) => 
{
    GD.Print($"State {state} timed out");
};

stateMachine.TransitionTriggered += (from, to) => 
{
    GD.Print($"Transition triggered: {from} -> {to}");
};

stateMachine.TimeoutBlocked += (state) => 
{
    GD.Print($"Timeout was blocked in state {state} due to locking");
};
```

### Manual State Changes

```csharp
// Try to change state manually
if (stateMachine.TryChangeState(PlayerState.Dead))
{
    GD.Print("Successfully changed to Dead state");
}

// Change with condition
stateMachine.TryChangeState(
    PlayerState.PowerUp,
    condition: () => hasPowerUpAvailable,
    data: powerUpType
);

// Go back to previous state
stateMachine.TryGoBack();
```

### State Queries

```csharp
// Check current state
if (stateMachine.IsCurrentState(PlayerState.Jumping))
{
    // Do something
}

// Check by tag
if (stateMachine.IsInStateWithTag("airborne"))
{
    // Player is in air
}

// Get state by tag
var airborneState = stateMachine.GetStateWithTag("airborne");

// Check if can enter state
if (stateMachine.CanEnterState(PlayerState.Attacking))
{
    // Attack is possible
}

// Time queries
float timeInState = stateMachine.GetStateTime();
float timeRemaining = stateMachine.GetRemainingTime();
float progress = stateMachine.GetTimeoutProgress(); // 0-1
```

### Pause/Resume

```csharp
// Pause the state machine
stateMachine.Pause();

// Resume
stateMachine.Resume();

// Resume and reset time
stateMachine.Resume(resetTime: true);

// Check if active
if (stateMachine.IsActive())
{
    // State machine is running
}
```

---

## Complete Examples

### Example 1: Simple Enemy AI

```csharp
public partial class Enemy : CharacterBody2D
{
    public enum EnemyState { Patrol, Chase, Attack, Retreat, Dead }
    
    private StateMachine<EnemyState> fsm;
    private Node2D player;
    private float health = 100;
    
    public override void _Ready()
    {
        fsm = new StateMachine<EnemyState>();
        player = GetNode<Node2D>("/root/Player");
        
        // Setup states
        SetupPatrolState();
        SetupChaseState();
        SetupAttackState();
        SetupRetreatState();
        SetupDeadState();
        
        // Setup transitions
        SetupTransitions();
        
        fsm.Start();
    }
    
    private void SetupPatrolState()
    {
        var state = fsm.AddState(EnemyState.Patrol);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.SetEnter(() => GD.Print("Started patrolling"));
        state.SetUpdate((delta) => 
        {
            // Patrol logic
            MoveAndSlide();
        });
    }
    
    private void SetupChaseState()
    {
        var state = fsm.AddState(EnemyState.Chase);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.SetUpdate((delta) => 
        {
            // Chase player
            var direction = (player.GlobalPosition - GlobalPosition).Normalized();
            Velocity = direction * 150;
            MoveAndSlide();
        });
    }
    
    private void SetupAttackState()
    {
        var state = fsm.AddState(EnemyState.Attack);
        state.SetTimeout(1.0f); // Attack duration
        state.SetRestart(EnemyState.Chase);
        state.SetLockMode(FSMLockMode.Full); // Can't interrupt attack
        state.SetEnter(() => 
        {
            GD.Print("Attacking!");
            // Play attack animation
        });
    }
    
    private void SetupRetreatState()
    {
        var state = fsm.AddState(EnemyState.Retreat);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.SetUpdate((delta) => 
        {
            // Run away from player
            var direction = (GlobalPosition - player.GlobalPosition).Normalized();
            Velocity = direction * 200;
            MoveAndSlide();
        });
    }
    
    private void SetupDeadState()
    {
        var state = fsm.AddState(EnemyState.Dead);
        state.SetLockMode(FSMLockMode.Full);
        state.SetEnter(() => 
        {
            GD.Print("Enemy died");
            // Play death animation, disable collision, etc.
        });
    }
    
    private void SetupTransitions()
    {
        // Patrol -> Chase when player is close
        fsm.AddTransition(EnemyState.Patrol, EnemyState.Chase)
            .SetCondition(fsm => GlobalPosition.DistanceTo(player.GlobalPosition) < 300);
        
        // Chase -> Attack when in range
        fsm.AddTransition(EnemyState.Chase, EnemyState.Attack)
            .SetCondition(fsm => GlobalPosition.DistanceTo(player.GlobalPosition) < 50);
        
        // Chase -> Patrol when player is far
        fsm.AddTransition(EnemyState.Chase, EnemyState.Patrol)
            .SetCondition(fsm => GlobalPosition.DistanceTo(player.GlobalPosition) > 400);
        
        // Any state -> Retreat when low health
        fsm.AddGlobalTransition(EnemyState.Retreat)
            .SetPriority(1)
            .SetCondition(fsm => health < 30 && health > 0);
        
        // Retreat -> Chase when health recovered
        fsm.AddTransition(EnemyState.Retreat, EnemyState.Chase)
            .SetCondition(fsm => health > 50);
        
        // Any state -> Dead when health is 0
        fsm.AddGlobalTransition(EnemyState.Dead)
            .SetPriority(0)
            .SetCondition(fsm => health <= 0);
    }
    
    public override void _PhysicsProcess(double delta)
    {
        fsm.Process(FSMProcessMode.Physics, delta);
    }
    
    public void TakeDamage(float damage)
    {
        health -= damage;
    }
}
```

### Example 2: Player Controller with Complex Movement

```csharp
public partial class Player : CharacterBody2D
{
    public enum PlayerState 
    { 
        Idle, Walking, Running, Jumping, Falling, 
        WallSliding, Dashing, Attacking, Dead 
    }
    
    private StateMachine<PlayerState> fsm;
    
    [Export] public float WalkSpeed = 150f;
    [Export] public float RunSpeed = 250f;
    [Export] public float JumpVelocity = -400f;
    
    private bool canDash = true;
    private float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
    
    public override void _Ready()
    {
        fsm = new StateMachine<PlayerState>();
        
        // Ground states
        SetupIdleState();
        SetupWalkingState();
        SetupRunningState();
        
        // Air states
        SetupJumpingState();
        SetupFallingState();
        SetupWallSlidingState();
        
        // Special states
        SetupDashingState();
        SetupAttackingState();
        
        SetupTransitions();
        
        // Subscribe to events
        fsm.StateChanged += (from, to) => 
        {
            GD.Print($"Player: {from} -> {to}");
        };
        
        fsm.Start();
    }
    
    private void SetupIdleState()
    {
        var state = fsm.AddState(PlayerState.Idle);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.AddTag("grounded");
        
        state.SetEnter(() => 
        {
            GD.Print("Idle");
        });
        
        state.SetUpdate((delta) => 
        {
            ApplyGravity(delta);
            MoveAndSlide();
        });
    }
    
    private void SetupWalkingState()
    {
        var state = fsm.AddState(PlayerState.Walking);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.AddTag("grounded");
        state.AddTag("moving");
        
        state.SetUpdate((delta) => 
        {
            var direction = Input.GetAxis("move_left", "move_right");
            Velocity = new Vector2(direction * WalkSpeed, Velocity.Y);
            ApplyGravity(delta);
            MoveAndSlide();
        });
    }
    
    private void SetupRunningState()
    {
        var state = fsm.AddState(PlayerState.Running);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.AddTag("grounded");
        state.AddTag("moving");
        
        state.SetUpdate((delta) => 
        {
            var direction = Input.GetAxis("move_left", "move_right");
            Velocity = new Vector2(direction * RunSpeed, Velocity.Y);
            ApplyGravity(delta);
            MoveAndSlide();
        });
    }
    
    private void SetupJumpingState()
    {
        var state = fsm.AddState(PlayerState.Jumping);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.AddTag("airborne");
        state.SetMinTime(0.1f); // Minimum jump time
        
        state.SetEnter(() => 
        {
            Velocity = new Vector2(Velocity.X, JumpVelocity);
        });
        
        state.SetUpdate((delta) => 
        {
            var direction = Input.GetAxis("move_left", "move_right");
            Velocity = new Vector2(direction * WalkSpeed, Velocity.Y);
            ApplyGravity(delta);
            MoveAndSlide();
        });
    }
    
    private void SetupFallingState()
    {
        var state = fsm.AddState(PlayerState.Falling);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.AddTag("airborne");
        
        state.SetUpdate((delta) => 
        {
            var direction = Input.GetAxis("move_left", "move_right");
            Velocity = new Vector2(direction * WalkSpeed, Velocity.Y);
            ApplyGravity(delta);
            MoveAndSlide();
        });
    }
    
    private void SetupWallSlidingState()
    {
        var state = fsm.AddState(PlayerState.WallSliding);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.AddTag("airborne");
        
        state.SetUpdate((delta) => 
        {
            Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y, 50)); // Slow fall
            ApplyGravity(delta);
            MoveAndSlide();
        });
    }
    
    private void SetupDashingState()
    {
        var state = fsm.AddState(PlayerState.Dashing);
        state.SetProcessMode(FSMProcessMode.Physics);
        state.SetTimeout(0.2f);
        state.SetLockMode(FSMLockMode.Full); // Can't cancel dash
        
        state.SetEnter(() => 
        {
            var direction = Input.GetAxis("move_left", "move_right");
            if (Mathf.Abs(direction) < 0.1f) direction = 1; // Default right
            Velocity = new Vector2(direction * 500, 0);
            canDash = false;
        });
        
        state.SetCallback(() => 
        {
            // Dash completed, return to appropriate state
            if (IsOnFloor())
                fsm.TryChangeState(PlayerState.Idle);
            else
                fsm.TryChangeState(PlayerState.Falling);
        });
    }
    
    private void SetupAttackingState()
    {
        var state = fsm.AddState(PlayerState.Attacking);
        state.SetTimeout(0.4f);
        state.SetLockMode(FSMLockMode.Full);
        
        state.SetEnter(() => 
        {
            GD.Print("Attack!");
        });
        
        state.SetCallback(() => 
        {
            fsm.TryChangeState(PlayerState.Idle);
        });
    }
    
    private void SetupTransitions()
    {
        // Idle <-> Walking
        fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
            .SetCondition(fsm => 
            {
                var dir = Input.GetAxis("move_left", "move_right");
                return Mathf.Abs(dir) > 0.1f && !Input.IsActionPressed("sprint");
            });
        
        fsm.AddTransition(PlayerState.Walking, PlayerState.Idle)
            .SetCondition(fsm => 
            {
                var dir = Input.GetAxis("move_left", "move_right");
                return Mathf.Abs(dir) < 0.1f;
            });
        
        // Walking -> Running
        fsm.AddTransition(PlayerState.Walking, PlayerState.Running)
            .SetCondition(fsm => Input.IsActionPressed("sprint"));
        
        // Running -> Walking
        fsm.AddTransition(PlayerState.Running, PlayerState.Walking)
            .SetCondition(fsm => !Input.IsActionPressed("sprint"));
        
        // Ground -> Jumping
        var groundStates = new[] { PlayerState.Idle, PlayerState.Walking, PlayerState.Running };
        fsm.AddTransitions(groundStates, PlayerState.Jumping,
            fsm => Input.IsActionJustPressed("jump") && IsOnFloor());
        
        // Jumping -> Falling
        fsm.AddTransition(PlayerState.Jumping, PlayerState.Falling)
            .SetCondition(fsm => Velocity.Y > 0);
        
        // Falling -> Grounded states
        fsm.AddTransition(PlayerState.Falling, PlayerState.Idle)
            .SetCondition(fsm => IsOnFloor());
        
        // Air -> Wall Sliding
        var airStates = new[] { PlayerState.Jumping, PlayerState.Falling };
        fsm.AddTransitions(airStates, PlayerState.WallSliding,
            fsm => IsOnWall() && !IsOnFloor());
        
        // Wall Sliding -> Falling
        fsm.AddTransition(PlayerState.WallSliding, PlayerState.Falling)
            .SetCondition(fsm => !IsOnWall());
        
        // Dash from most states
        var dashableStates = new[] { 
            PlayerState.Idle, PlayerState.Walking, PlayerState.Running,
            PlayerState.Jumping, PlayerState.Falling 
        };
        foreach (var state in dashableStates)
        {
            fsm.AddTransition(state, PlayerState.Dashing)
                .SetPriority(1)
                .SetCondition(fsm => Input.IsActionJustPressed("dash") && canDash);
        }
        
        // Attack from grounded states
        fsm.AddTransitions(groundStates, PlayerState.Attacking,
            fsm => Input.IsActionJustPressed("attack"));
    }
    
    public override void _PhysicsProcess(double delta)
    {
        fsm.Process(FSMProcessMode.Physics, delta);
        
        // Reset dash when grounded
        if (IsOnFloor())
            canDash = true;
    }
    
    private void ApplyGravity(double delta)
    {
        if (!IsOnFloor())
            Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * (float)delta);
    }
}
```

---

## Debugging Tips

```csharp
// Print current transition
GD.Print(fsm.DebugCurrentTransition());
// Output: "Idle -> Walking"

// Print all transitions
GD.Print(fsm.DebugAllTransitions());

// Print all states
GD.Print(fsm.DebugAllStates());

// Check state time
GD.Print($"Time in state: {fsm.GetStateTime()}");

// Check if transition exists
if (fsm.HasTransition(PlayerState.Idle, PlayerState.Walking))
{
    GD.Print("Transition exists");
}
```

---

## Best Practices

1. **Use Tags**: Tag states for easy querying (e.g., "airborne", "invulnerable")
2. **Set Process Modes**: Use Physics mode for movement, Idle for UI/animations
3. **Use Priorities**: Lower numbers for more important transitions
4. **Lock States**: Lock states that shouldn't be interrupted (attacks, cutscenes)
5. **Use Events**: Event-based transitions for complex game events
6. **Pass Data**: Use transition data for context (damage amounts, knockback, etc.)
7. **Minimum Times**: Prevent state flickering with MinTime
8. **Subscribe to Events**: Use StateChanged, TransitionTriggered for debugging and game logic

---

## Common Patterns

### Cooldown Pattern
```csharp
var attackState = fsm.AddState(State.Attack);
attackState.SetTimeout(0.5f);
attackState.SetRestart(State.Cooldown);

var cooldownState = fsm.AddState(State.Cooldown);
cooldownState.SetTimeout(1.0f);
cooldownState.SetRestart(State.Idle);
```

### Charge-Up Pattern
```csharp
var chargeState = fsm.AddState(State.Charging);
chargeState.SetMinTime(1.0f); // Must charge for 1 second

fsm.AddTransition(State.Charging, State.PowerAttack)
    .SetCondition(fsm => !Input.IsActionPressed("charge"));
```

### State History Pattern
```csharp
// Go back to previous state
if (fsm.TryGoBack())
{
    GD.Print("Returned to previous state");
}
```

---

This guide covers the core functionality of the state machine. Experiment with different configurations to find what works best for your game!
