# 🎮 Finite State Machine for Godot (C#)

A powerful, feature-rich Finite State Machine library for Godot 4 C# projects. This FSM provides an elegant and type-safe way to manage complex state-driven behaviors with support for transitions, events, timeouts, cooldowns, history tracking, and much more.

## ✨ Features

- **Type-Safe**: Generic implementation using C# enums for state IDs
- **Flexible Transitions**: Condition-based, event-driven, or time-based transitions
- **Guards & Conditions**: Pre-validation and transition logic separation
- **Cooldowns**: Built-in cooldown system for states and transitions
- **State Locking**: Prevent unwanted transitions with flexible lock modes
- **State History**: Navigate backward through state history
- **Global Transitions**: Define transitions that work from any state
- **State Templates**: Reusable state configurations
- **Timeout System**: Automatic state timeouts with custom handlers
- **Event System**: Event-driven transitions and listeners
- **Process Modes**: Support for `_Process` (Idle) and `_PhysicsProcess` (Fixed) updates
- **Priority System**: Control transition evaluation order
- **Data Storage**: Per-state and global data storage
- **Tags**: Organize and query states using tags

## 📦 Installation

1. Copy all `.cs` files into your Godot C# project
2. Ensure your project is using .NET and C# scripting
3. The namespace is `FiniteStateMachine`

## 🚀 Quick Start

### Basic Example

```csharp
using Godot;
using FiniteStateMachine;

public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Jumping
}

public partial class Player : CharacterBody2D
{
    private StateMachine<PlayerState> fsm;

    public override void _Ready()
    {
        // Create the state machine
        fsm = new StateMachine<PlayerState>();

        // Add states with callbacks
        fsm.AddState(PlayerState.Idle)
            .OnEnter(() => GD.Print("Entered Idle"))
            .OnUpdate(delta => GD.Print("Idling..."))
            .OnExit(() => GD.Print("Exited Idle"));

        fsm.AddState(PlayerState.Walking)
            .OnEnter(() => GD.Print("Started Walking"));

        // Add transitions with conditions
        fsm.AddState(PlayerState.Idle)
            .AddTransition(PlayerState.Walking)
            .SetCondition(sm => Input.IsActionPressed("move_right"));

        // Set initial state and start
        fsm.SetInitialId(PlayerState.Idle);
        fsm.Start();
    }

    public override void _Process(double delta)
    {
        fsm.Update((float)delta);
    }
}
```

### Alternative: Using Configuration Methods

For cleaner, more organized code, you can use the `ConfigureState` and `ConfigureTransition` methods:

```csharp
public override void _Ready()
{
    fsm = new StateMachine<PlayerState>();

    // Configure states with a lambda
    fsm.ConfigureState(PlayerState.Idle, state => {
        state.OnEnter(() => GD.Print("Entered Idle"))
             .OnUpdate(delta => HandleIdleUpdate(delta))
             .OnExit(() => GD.Print("Exited Idle"))
             .MinDuration(0.1f)
             .AddTags("grounded");
    });

    // Configure transitions separately
    fsm.ConfigureTransition(PlayerState.Idle, PlayerState.Walking, t => {
        t.SetCondition(sm => IsMoving())
         .SetPriority(5)
         .OnTrigger(() => GD.Print("Started walking!"));
    });

    fsm.SetInitialId(PlayerState.Idle);
    fsm.Start();
}
```

## 📖 Core Concepts

### State & Transition Configuration

The FSM provides two approaches for building your state machine:

#### Method 1: Inline Chaining (Quick & Direct)
```csharp
// Add and configure in one go
fsm.AddState(PlayerState.Idle)
    .OnEnter(() => PlayIdleAnimation())
    .OnUpdate(delta => CheckInput())
    .AddTransition(PlayerState.Walking)
        .SetCondition(sm => IsMoving());
```

#### Method 2: Configuration Methods (Organized & Readable)
```csharp
// Configure state with a lambda - auto-creates if doesn't exist
fsm.ConfigureState(PlayerState.Idle, state => {
    state.OnEnter(() => PlayIdleAnimation())
         .OnUpdate(delta => CheckInput())
         .MinDuration(0.1f)
         .AddTags("grounded", "interruptible");
});

// Configure transition separately - auto-creates if doesn't exist
fsm.ConfigureTransition(PlayerState.Idle, PlayerState.Walking, transition => {
    transition.SetCondition(sm => IsMoving())
              .SetPriority(5)
              .SetCooldown(0.1f)
              .OnTrigger(() => GD.Print("Walking!"));
});

// Configure global transitions
fsm.ConfigureGlobalTransition(PlayerState.Death, transition => {
    transition.SetCondition(sm => health <= 0)
              .HighestPriority();
});
```

**Benefits of Configuration Methods:**
- Auto-creates states/transitions if they don't exist
- More readable for complex setups
- Easier to organize in separate methods
- No need to store intermediate references

**Example: Organized Setup**
```csharp
private void InitializeStateMachine()
{
    fsm = new StateMachine<PlayerState>();
    
    ConfigureMovementStates();
    ConfigureCombatStates();
    ConfigureTransitions();
    
    fsm.SetInitialId(PlayerState.Idle);
    fsm.Start();
}

private void ConfigureMovementStates()
{
    fsm.ConfigureState(PlayerState.Idle, s => {
        s.OnEnter(() => sprite.Play("idle"))
         .OnUpdate(ApplyGravity)
         .AddTags("grounded");
    });
    
    fsm.ConfigureState(PlayerState.Walking, s => {
        s.OnEnter(() => sprite.Play("walk"))
         .OnUpdate(delta => MoveHorizontal(WalkSpeed))
         .AddTags("grounded", "moving");
    });
}

private void ConfigureTransitions()
{
    fsm.ConfigureTransition(PlayerState.Idle, PlayerState.Walking, t => {
        t.SetCondition(sm => IsMoving() && !IsRunning());
    });
    
    fsm.ConfigureTransition(PlayerState.Walking, PlayerState.Idle, t => {
        t.SetCondition(sm => !IsMoving());
    });
}
```

### Bulk Transition Configuration

The FSM provides helper methods for creating multiple transitions at once:

```csharp
// Add transitions from multiple states to one target
var groundedStates = new[] { 
    PlayerState.Idle, 
    PlayerState.Walking, 
    PlayerState.Running 
};
fsm.AddTransitions(groundedStates, PlayerState.Jumping, sm => JumpPressed());

// Add transitions from all states with a specific tag
fsm.AddTagTransition("grounded", PlayerState.Jumping, sm => JumpPressed());

// Event-based tag transitions
fsm.AddTagTransition("interruptible", PlayerState.Stunned, "stunned");

// Enum event-based tag transitions
public enum GameEvent { Stunned, Healed, PowerUp }
fsm.AddTagTransition<GameEvent>("interruptible", PlayerState.Stunned, GameEvent.Stunned);

// Reset transition (return to initial state)
fsm.AddResetTransition(PlayerState.GameOver);

// Self transition (restart same state)
fsm.AddSelfTransition(PlayerState.Attacking)
   .SetCondition(sm => CanCombo())
   .OnTrigger(() => ResetCombo());
```

### Removing States and Transitions

```csharp
// Remove a specific transition
fsm.RemoveTransition(PlayerState.Idle, PlayerState.Walking);

// Remove a global transition
fsm.RemoveGlobalTransition(PlayerState.Death);

// Clear all transitions from a state
fsm.ClearTransitionsFrom(PlayerState.Idle);

// Clear ALL transitions (keeps states)
fsm.ClearTransitions();

// Clear all global transitions
fsm.ClearGlobalTransitions();

// Remove a state entirely (requires at least 2 states)
fsm.RemoveState(PlayerState.Obsolete);
// Note: This also removes all transitions to/from this state
```

### States

States represent distinct behavioral modes. Each state can have:
- **Enter/Exit callbacks**: Called when entering or leaving the state
- **Update callback**: Called every frame while in the state
- **Minimum duration**: Prevent premature transitions
- **Timeout**: Automatically transition after a duration
- **Lock mode**: Prevent transitions
- **Cooldown**: Prevent re-entry for a duration
- **Tags**: Organize states by category
- **Data**: Store state-specific data

```csharp
fsm.AddState(PlayerState.Attacking)
    .OnEnter(() => PlayAttackAnimation())
    .OnUpdate(delta => UpdateAttack(delta))
    .OnExit(() => ResetAttack())
    .MinDuration(0.3f)              // Must stay at least 0.3s
    .TimeoutAfter(1.0f, PlayerState.Idle)  // Auto-return to Idle after 1s
    .SetCooldown(2.0f)              // Can't attack again for 2s
    .AddTags("combat", "active")
    .SetData("damage", 10);
```

### Transitions

Transitions define how to move between states. They support:
- **Conditions**: Logic that must be true to transition
- **Guards**: Pre-checks evaluated before time requirements
- **Events**: Trigger on named events
- **Priority**: Control evaluation order
- **Cooldowns**: Prevent rapid re-triggering
- **Minimum time override**: Per-transition timing requirements

```csharp
// Condition-based transition
fsm..AddTransition(PlayerState.Idle, PlayerState.Walking)
    .SetCondition(sm => velocity.Length() > 0);

// Event-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Jumping)
    .OnEvent("jump");

// Guarded transition with priority
fsm.AddTransition(PlayerState.Walking, PlayerState.Attacking)
    .SetGuard(sm => HasWeapon())        // Checked first
    .SetCondition(sm => Input.IsActionJustPressed("attack"))
    .SetPriority(10)                     // Higher priority = checked first
    .SetCooldown(0.5f);                 // 500ms cooldown
```

### Guard vs Condition

- **Guard**: Pre-validation check (e.g., "Is player alive?", "Has ammo?")
  - Evaluated FIRST, before time requirements
  - Return `false` to reject the transition early
  - Used in both regular and event-based transitions
  
- **Condition**: The actual transition trigger (e.g., "Is attack pressed?")
  - Evaluated AFTER guard and time requirements
  - Return `true` to trigger the transition
  - Only used in regular (non-event) transitions

```csharp
fsm.AddTransition(PlayerState.Idle, PlayerState.Shooting)
    .SetGuard(sm => HasAmmo())  // Must have ammo
    .SetCondition(sm => Input.IsActionPressed("shoot"));  // Then check input
```

### Process Modes

Control when state updates occur:

```csharp
// Update in _Process (default)
fsm.AddState(PlayerState.Idle)
    .SetProcessMode(FSMProcessMode.Idle);

// Update in _PhysicsProcess
fsm.AddState(PlayerState.Falling)
    .SetProcessMode(FSMProcessMode.Fixed);
```

Then call the appropriate update method:

```csharp
public override void _Process(double delta)
{
    fsm.UpdateIdle((float)delta);    // Updates Idle mode states
}

public override void _PhysicsProcess(double delta)
{
    fsm.UpdateFixed((float)delta);   // Updates Fixed mode states
}
```

**Update Method Variants:**
- `UpdateIdle(float delta)` - for `_Process`
- `UpdateFixed(float delta)` - for `_PhysicsProcess`

**Note:** States will only update during the process mode they're configured for. Cooldowns update according to `SetCooldownTimersProcessMode()`.


### Manual Transitions

Sometimes you need to force a transition programmatically:

```csharp
// Simple manual transition (ignores conditions)
fsm.TryTransitionTo(PlayerState.Jumping);

// Transition with optional condition check
fsm.TryTransitionTo(PlayerState.Attacking, 
    condition: () => HasEnoughStamina());

// Transition with data payload
fsm.TryTransitionTo(PlayerState.Damaged, 
    data: new DamageInfo { Amount = 25, Type = "fire" });

// Retrieve per-transition data in the new state
var damageInfo = fsm.GetPerTransitionData<DamageInfo>();
if (damageInfo != null)
{
    ApplyDamage(damageInfo.Amount);
}

// Note: TryTransitionTo respects minimum time requirements
// Returns true if transition succeeded, false otherwise
```

### Queued Transitions

Transitions that occur during another transition are automatically queued:

```csharp
// If called during a transition, this will queue
fsm.TryTransitionTo(PlayerState.Attacking);
fsm.TryTransitionTo(PlayerState.Idle);  // Queued

// The FSM processes queued transitions automatically
// Maximum queue size: 20 (prevents infinite loops)
```

### Restart Current State

```csharp
// Restart without calling Enter/Exit
fsm.RestartCurrentState(callEnter: false, callExit: false);

// Restart with Enter callback
fsm.RestartCurrentState(callEnter: true, callExit: false);

// Full restart with both callbacks
fsm.RestartCurrentState(callEnter: true, callExit: true);

// Note: Cannot restart if state is locked
```



Prevent unwanted transitions:

```csharp
// Full lock: No transitions allowed, timeout triggers TimeoutExpired callback
fsm.AddState(PlayerState.Stunned)
    .Lock(FSMLockMode.Full)
    .TimeoutAfter(2.0f, PlayerState.Idle)
    .OnTimeoutExpired(() => GD.Print("Stun expired!"));

// Transition lock: Only timeout can transition
fsm.AddState(PlayerState.Attacking)
    .Lock(FSMLockMode.Transition);

// Unlock
someState.Unlock();
```

### Global Transitions

Define transitions that work from any state:

```csharp
// Can transition to Death from any state
fsm.AddGlobalTransition(PlayerState.Death)
    .SetCondition(sm => health <= 0)
    .HighestPriority();  // Checked before other transitions
```

### State History

Track and navigate through previous states with the built-in history system:

```csharp
// === Basic Navigation ===
// Go back to the previous state
if (fsm.CanGoBack())
{
    fsm.GoBack();  // Same as GoBack(1)
}

// Go back multiple states
if (fsm.CanGoBack(3))
{
    fsm.GoBack(3);  // Go back 3 states
}

// Go back to a specific state (searches history)
if (fsm.GoBackToState(PlayerState.Idle))
{
    GD.Print("Returned to Idle state");
}

// === Peeking at History ===
// Peek at previous state without transitioning
var prevState = fsm.PeekBackState();      // 1 step back
var older = fsm.PeekBackState(3);         // 3 steps back

// Find how many steps back a state is
int stepsBack = fsm.FindInHistory(PlayerState.Idle);
if (stepsBack >= 0)
{
    GD.Print($"Idle state is {stepsBack} steps back");
}

// === Accessing History ===
// Get full history (most recent first)
var history = fsm.StateHistory.GetHistory();
foreach (var entry in history)
{
    GD.Print($"State: {entry.StateId}");
    GD.Print($"  Time Spent: {entry.TimeSpent:F2}s");
    GD.Print($"  Timestamp: {entry.TimeStamp:F2}s");
}

// Get recent history (e.g., last 5 states)
var recent = fsm.StateHistory.GetRecentHistory(5);
foreach (var entry in recent)
{
    GD.Print($"{entry.StateId} ({entry.TimeSpent:F2}s)");
}

// Get specific history entry
if (fsm.StateHistory.CurrentSize > 0)
{
    var entry = fsm.StateHistory.GetEntry(0);  // Index 0 is oldest
}

// === History Configuration ===
// Enable/disable history tracking
fsm.SetHistoryActive(true);   // Default: true
fsm.SetHistoryActive(false);  // Stops recording new entries

// Check if history is active
if (fsm.StateHistory.IsActive)
{
    GD.Print("History tracking enabled");
}

// Set history capacity (default: 20)
fsm.StateHistory.SetCapacity(50);  // Keep last 50 states

// Get current history size
int size = fsm.StateHistory.CurrentSize;
GD.Print($"History contains {size} entries");

// Get capacity
int capacity = fsm.StateHistory.Capacity;

// Clear all history
fsm.StateHistory.ClearHistory();

// Remove range of entries
fsm.StateHistory.RemoveRange(startIndex: 0, count: 5);
```

**History Use Cases:**
- **Undo System**: Let players rewind actions
- **Debug Tool**: See what states led to a bug
- **AI Behavior**: Track NPC decision patterns
- **Analytics**: Analyze player behavior patterns
- **Breadcrumb Trail**: Show path through states

**Important Notes:**
- History doesn't record when using `GoBack()` (prevents recursion)
- `Reset()` clears all history
- History uses memory - set appropriate capacity
- Each entry stores: StateId, TimeSpent, Timestamp


### Events

Use events for complex state changes and custom event handling:

```csharp
// Define transition on event
fsm.AddTransition(PlayerState.Idle, PlayerState.Rolling)
    .OnEvent("dodge");

// Using enum for type-safety
public enum PlayerEvent { Dodge, Attack, Jump }

fsm.AddTransition(PlayerState.Idle, PlayerState.Rolling)
    .OnEvent(PlayerEvent.Dodge);

// Trigger the event
fsm.TriggerEvent("dodge");
// OR
fsm.TriggerEvent(PlayerEvent.Dodge);

// Subscribe to state machine lifecycle events
fsm.StateChanged += (from, to) => GD.Print($"Changed: {from} -> {to}");
fsm.StateTimeout += state => GD.Print($"{state} timed out");
fsm.TransitionTriggered += (from, to) => GD.Print($"Transition: {from} -> {to}");
fsm.TimeoutBlocked += state => GD.Print($"{state} timeout blocked by cooldown");

// Custom event listeners (independent of transitions)
fsm.OnEvent("player_hit", () => {
    GD.Print("Player was hit!");
    FlashSprite();
});

// Type-safe event listeners
fsm.OnEvent(GameEvent.PowerUp, () => {
    GD.Print("Power up collected!");
});

// Multiple listeners for same event
fsm.OnEvent("player_hit", () => PlayHitSound());
fsm.OnEvent("player_hit", () => SpawnBloodEffect());

// Trigger custom events
fsm.TriggerEvent("player_hit");

// Remove event listener
Action callback = () => GD.Print("Hit!");
fsm.OnEvent("player_hit", callback);
fsm.RemoveEventListener("player_hit", callback);

// Clear all event listeners
fsm.ClearEventListeners();
```

**Event Processing Order:**
1. Event-based transitions are checked first
2. Then custom event listeners are invoked
3. Events are processed in the order they're triggered


### State Templates

Reuse common state configurations:

```csharp
// Create a template for combat states
var combatTemplate = new StateTemplate<PlayerState>()
    .WithEnter(() => StartCombat())
    .WithExit(() => EndCombat())
    .WithTags("combat")
    .WithLock(FSMLockMode.Transition)
    .WithMinDuration(0.2f);

// Apply to multiple states
fsm.AddState(PlayerState.Attacking).ApplyTemplate(combatTemplate);
fsm.AddState(PlayerState.Blocking).ApplyTemplate(combatTemplate);
fsm.AddState(PlayerState.Parrying).ApplyTemplate(combatTemplate);
```

### Data Storage

Store and retrieve data at state and global levels:

```csharp
// Global data (accessible everywhere)
fsm.SetData("player_health", 100);
if (fsm.TryGetData<int>("player_health", out int health))
{
    GD.Print($"Health: {health}");
}

// State-specific data
fsm.GetState(PlayerState.Attacking)
    .SetData("combo_count", 0)
    .SetData("damage_multiplier", 1.5f);

// Per-transition data (passed during transition)
fsm.TryTransitionTo(PlayerState.Damaged, data: new DamageInfo(10, "fire"));
var damageInfo = fsm.GetPerTransitionData<DamageInfo>();
```

### Cooldowns

Prevent rapid state/transition re-triggering with the built-in cooldown system:

```csharp
// === State Cooldowns ===
// Set state cooldown - can't enter this state again for 3 seconds
fsm.AddState(PlayerState.Dashing)
    .SetCooldown(3.0f);

// Check state cooldown
if (fsm.IsStateOnCooldown(PlayerState.Dashing))
{
    GD.Print("Can't dash yet!");
    
    // Get cooldown details from state object
    var state = fsm.GetState(PlayerState.Dashing);
    float remaining = state.Cooldown.GetRemaining();
    float progress = state.Cooldown.GetProgress();  // 0.0 to 1.0
    GD.Print($"Cooldown: {remaining:F1}s ({progress*100:F0}%)");
}

// Reset specific state cooldown
fsm.ResetStateCooldown(PlayerState.Dashing);

// === Transition Cooldowns ===
// Set transition cooldown - can't use this transition for 1 second
fsm.AddTransition(PlayerState.Idle, PlayerState.Attacking)
    .SetCooldown(1.0f);

// Check transition cooldown
if (fsm.IsTransitionOnCooldown(PlayerState.Idle, PlayerState.Attacking))
{
    GD.Print("Attack on cooldown!");
}

// Reset specific transition cooldown
fsm.ResetTransitionCooldown(PlayerState.Idle, PlayerState.Attacking);

// === Managing All Cooldowns ===
// Reset all cooldowns (states and transitions)
fsm.ResetAllCooldowns();

// Get count of active cooldowns
int activeCount = fsm.GetActiveCooldownCount();
GD.Print($"{activeCount} cooldowns currently active");

// === Cooldown Process Mode ===
// Set when cooldowns update (default: Idle)
fsm.SetCooldownTimersProcessMode(FSMProcessMode.Idle);    // Update in _Process
fsm.SetCooldownTimersProcessMode(FSMProcessMode.Fixed);   // Update in _PhysicsProcess

// === Advanced Cooldown Access ===
// Access cooldown directly from state
var dashState = fsm.GetState(PlayerState.Dashing);
if (dashState.Cooldown.IsActive)
{
    float normalizedRemaining = dashState.Cooldown.GetNormalizedRemaining();  // 0.0 to 1.0
    bool complete = dashState.Cooldown.IsComplete();
}
```

**Cooldown Tips:**
- State cooldowns start when you EXIT the state
- Transition cooldowns start when the transition is USED
- Cooldowns prevent spam and add game balance
- Use longer cooldowns for powerful abilities
- Check cooldowns before showing UI prompts

## 🛠️ Advanced Features

### Custom Logger

Implement custom logging:

```csharp
public class GodotLogger : ILogger
{
    public void LogError(string text) => GD.PrintErr($"[FSM ERROR] {text}");
    public void LogWarning(string text) => GD.Print($"[FSM WARNING] {text}");
}

var fsm = new StateMachine<PlayerState>(new GodotLogger());
```

### Queued Transitions

Queue multiple transitions:

```csharp
fsm.QueueTransition(PlayerState.Attacking);
fsm.QueueTransition(PlayerState.Idle);
// Processes queued transitions automatically
```

### State Queries

The FSM provides extensive query capabilities:

```csharp
// === Current State Queries ===
// Check current state
if (fsm.IsCurrentState(PlayerState.Attacking))
{
    GD.Print("Currently attacking!");
}

// Get current state ID
var currentId = fsm.GetCurrentId();

// Get current state name
var stateName = fsm.GetCurrentStateName();  // Returns enum as string

// Get current state object
var state = fsm.GetState(PlayerState.Idle);
if (state != null)
{
    GD.Print($"Has {state.Tags.Count} tags");
}

// === Previous State Queries ===
// Check previous state
if (fsm.IsPreviousState(PlayerState.Jumping))
{
    GD.Print("Just landed!");
}

// Get previous state ID
var prevId = fsm.GetPreviousId();

// === Tag-based Queries ===
// Check if in state with tag
if (fsm.IsInStateWithTag("airborne"))
{
    GD.Print("Player is in the air!");
}

// Get first state with tag
var combatState = fsm.GetStateWithTag("combat");

// Get all states with tag
var groundedStates = fsm.GetStatesWithTag("grounded");
foreach (var state in groundedStates)
{
    GD.Print($"Grounded state: {state.Id}");
}

// === Timing Queries ===
// Get time spent in current state
float timeInState = fsm.GetStateTime();

// Get time spent in last state
float timeInLastState = fsm.GetLastStateTime();

// Get minimum time requirement
float minTime = fsm.GetMinStateTime();

// Check if minimum time exceeded
if (fsm.MinTimeExceeded())
{
    // Can transition now
}

// Get remaining timeout time
float remaining = fsm.GetRemainingTime();  // -1 if no timeout

// Get timeout progress (0.0 to 1.0)
float progress = fsm.GetTimeoutProgress();  // -1 if no timeout
if (progress >= 0)
{
    UpdateProgressBar(progress);
}

// === Transition Queries ===
// Check if transition exists
if (fsm.HasTransition(PlayerState.Idle, PlayerState.Walking))
{
    GD.Print("Can walk from idle");
}

// Check if global transition exists
if (fsm.HasGlobalTransition(PlayerState.Death))
{
    GD.Print("Death is a global transition");
}

// Check if any global transitions exist
if (fsm.HasAnyGlobalTransitions())
{
    GD.Print("Global transitions configured");
}

// Get available transitions from current state
var transitions = fsm.GetAvailableTransitions();
foreach (var t in transitions)
{
    GD.Print($"Can transition to: {t.To}");
}

// === Cooldown Queries ===
// Check state cooldown
if (fsm.IsStateOnCooldown(PlayerState.Dashing))
{
    GD.Print("Can't dash yet!");
}

// Check transition cooldown
if (fsm.IsTransitionOnCooldown(PlayerState.Idle, PlayerState.Attacking))
{
    GD.Print("Can't attack yet!");
}

// Get total active cooldowns
int activeCooldowns = fsm.GetActiveCooldownCount();
GD.Print($"Active cooldowns: {activeCooldowns}");

// === Status Queries ===
// Check if FSM is active (not paused)
if (fsm.IsActive())
{
    GD.Print("FSM is running");
}
```


### Timeout Progress

Track timeout progress:

```csharp
float progress = fsm.GetTimeoutProgress();  // 0.0 to 1.0
float remaining = fsm.GetRemainingTime();   // Seconds remaining

if (progress >= 0)
{
    UpdateProgressBar(progress);
}
```

### Pause, Resume, and Reset

Control FSM execution:

```csharp
// Pause the FSM (stops all updates)
fsm.Pause();

// Resume from pause
fsm.Resume();

// Toggle pause state
fsm.TogglePaused(true);   // Pause
fsm.TogglePaused(false);  // Resume

// Check if active (not paused)
if (fsm.IsActive())
{
    GD.Print("FSM is running");
}

// Reset to initial state (clears history)
fsm.Reset();

// Reset state time (keeps current state)
fsm.ResetStateTime();
```

**Use Cases:**
- `Pause()`: During game menus, cutscenes, or when player opens inventory
- `Reset()`: On player death, level restart, or game restart
- `ResetStateTime()`: When you want to "refresh" the current state's timer


## 📋 Best Practices

1. **Use Enums**: Always use enums for state IDs - it provides type safety and IDE autocomplete
2. **Tag Your States**: Use tags to group related states ("grounded", "airborne", "combat")
3. **Guards vs Conditions**: Use guards for prerequisites, conditions for triggers
4. **Lock Important States**: Prevent interruption of critical animations/actions
5. **Set Cooldowns**: Prevent spamming of abilities or rapid state cycling
6. **Use Templates**: Share common configurations across similar states
7. **Track History**: Enable history to implement "undo" or debug state flow
8. **Process Modes**: Use Fixed for physics-dependent states, Idle for visuals/UI
9. **Event-Driven**: Use events for player input or network messages
10. **Priority System**: Set priorities on transitions that should be checked first

## 🐛 Debugging Tips

```csharp
// Log all state changes
fsm.StateChanged += (from, to) => GD.Print($"{from} -> {to}");

// Check available transitions
var transitions = fsm.GetAvailableTransitions();
foreach (var t in transitions)
{
    GD.Print($"Can transition to: {t.To}");
}

// View state history
var history = fsm.StateHistory.GetRecentHistory(10);
foreach (var entry in history)
{
    GD.Print($"[{entry.TimeStamp:F2}s] {entry.StateId} ({entry.TimeSpent:F2}s)");
}

// Check cooldowns
int activeCooldowns = fsm.GetActiveCooldownCount();
GD.Print($"Active cooldowns: {activeCooldowns}");
```

## 📄 API Reference

### StateMachine\<T\> Methods

#### State Management
| Method | Description |
|--------|-------------|
| `AddState(T id)` | Create and add a new state, returns State\<T\> |
| `ConfigureState(T id, Action<State<T>>)` | Configure existing or new state with lambda |
| `GetState(T id)` | Get state object by ID |
| `GetStateWithTag(string tag)` | Get first state with specified tag |
| `GetStatesWithTag(string tag)` | Get all states with specified tag |
| `RemoveState(T id)` | Remove a state (requires at least 2 states) |
| `SetInitialId(T id)` | Set the starting state |
| `GetCurrentId()` | Get current state ID |
| `GetPreviousId()` | Get previous state ID |
| `GetCurrentStateName()` | Get current state as string |

#### Transition Management
| Method | Description |
|--------|-------------|
| `AddTransition(T from, T to)` | Add transition between states |
| `AddResetTransition(T from)` | Add transition to initial state |
| `AddSelfTransition(T id)` | Add self-loop transition |
| `AddTransitions(T[] from, T to, Predicate)` | Add transitions from multiple states |
| `AddTagTransition(string tag, T to, ...)` | Add transitions from tagged states |
| `AddGlobalTransition(T to)` | Add transition available from any state |
| `ConfigureTransition(T from, T to, Action<Transition<T>>)` | Configure transition with lambda |
| `ConfigureGlobalTransition(T to, Action<Transition<T>>)` | Configure global transition |
| `RemoveTransition(T from, T to)` | Remove specific transition |
| `RemoveGlobalTransition(T to)` | Remove global transition |
| `ClearTransitionsFrom(T id)` | Clear all transitions from a state |
| `ClearTransitions()` | Clear all transitions |
| `ClearGlobalTransitions()` | Clear all global transitions |
| `GetAvailableTransitions()` | Get transitions from current state |
| `HasTransition(T from, T to)` | Check if transition exists |
| `HasGlobalTransition(T to)` | Check if global transition exists |
| `HasAnyGlobalTransitions()` | Check if any global transitions exist |

#### Execution Control
| Method | Description |
|--------|-------------|
| `Start()` | Start the state machine |
| `Update(float delta)` | Update in _Process (calls UpdateIdle) |
| `UpdateIdle(float delta)` | Update Idle mode states |
| `FixedUpdate(float delta)` | Update in _PhysicsProcess (calls UpdateFixed) |
| `UpdateFixed(float delta)` | Update Fixed mode states |
| `Pause()` | Pause all updates |
| `Resume()` | Resume from pause |
| `TogglePaused(bool toggle)` | Set pause state |
| `IsActive()` | Check if not paused |
| `Reset()` | Return to initial state (clears history) |
| `RestartCurrentState(bool enter, bool exit)` | Restart current state with optional callbacks |
| `ResetStateTime()` | Reset current state's timer |

#### Manual Transitions
| Method | Description |
|--------|-------------|
| `TryTransitionTo(T to, Func<bool>, object)` | Attempt manual transition with optional condition and data |
| `GoBack()` | Return to previous state |
| `GoBack(int steps)` | Go back multiple states |
| `GoBackToState(T id)` | Return to specific state in history |
| `CanGoBack()` | Check if can go back 1 step |
| `CanGoBack(int steps)` | Check if can go back N steps |
| `PeekBackState()` | Peek 1 state back without transitioning |
| `PeekBackState(int steps)` | Peek N states back |
| `FindInHistory(T id)` | Find how many steps back a state is |

#### Event System
| Method | Description |
|--------|-------------|
| `TriggerEvent(string eventName)` | Trigger event-based transitions |
| `TriggerEvent<TEvent>(TEvent)` | Trigger event using enum |
| `OnEvent(string eventName, Action)` | Subscribe to custom event |
| `OnEvent<TEvent>(TEvent, Action)` | Subscribe using enum |
| `RemoveEventListener(string, Action)` | Remove event listener |
| `ClearEventListeners()` | Clear all custom listeners |

#### Data Storage
| Method | Description |
|--------|-------------|
| `SetData(string id, object value)` | Store global data |
| `TryGetData<TData>(string id, out TData)` | Retrieve global data |
| `GetPerTransitionData<TData>()` | Get data passed during transition |

#### State Queries
| Method | Description |
|--------|-------------|
| `IsCurrentState(T id)` | Check if in specific state |
| `IsPreviousState(T id)` | Check if previous state matches |
| `IsInStateWithTag(string tag)` | Check if current state has tag |
| `GetStateTime()` | Get time in current state |
| `GetLastStateTime()` | Get time in last state |
| `GetMinStateTime()` | Get minimum time requirement |
| `MinTimeExceeded()` | Check if min time exceeded |
| `GetRemainingTime()` | Get remaining timeout time (-1 if none) |
| `GetTimeoutProgress()` | Get timeout progress 0.0-1.0 (-1 if none) |

#### Cooldown Management
| Method | Description |
|--------|-------------|
| `IsStateOnCooldown(T id)` | Check state cooldown |
| `IsTransitionOnCooldown(T from, T to)` | Check transition cooldown |
| `ResetStateCooldown(T id)` | Reset state cooldown |
| `ResetTransitionCooldown(T from, T to)` | Reset transition cooldown |
| `ResetAllCooldowns()` | Reset all cooldowns |
| `GetActiveCooldownCount()` | Count active cooldowns |
| `SetCooldownTimersProcessMode(FSMProcessMode)` | Set when cooldowns update |

#### History Management
| Method | Description |
|--------|-------------|
| `SetHistoryActive(bool)` | Enable/disable history tracking |
| `StateHistory` | Access StateHistory\<T\> object |

#### Events (C# Events)
| Event | Description |
|-------|-------------|
| `StateChanged` | `Action<T from, T to>` - Fired on state change |
| `TransitionTriggered` | `Action<T from, T to>` - Fired on transition |
| `StateTimeout` | `Action<T state>` - Fired on state timeout |
| `TimeoutBlocked` | `Action<T state>` - Fired when timeout blocked by cooldown |

#### Lifecycle
| Method | Description |
|--------|-------------|
| `Dispose()` | Clean up resources (implements IDisposable) |

---

### State\<T\> Methods

#### Callbacks
| Method | Returns | Description |
|--------|---------|-------------|
| `OnEnter(Action)` | `State<T>` | Set enter callback |
| `OnUpdate(Action<float>)` | `State<T>` | Set update callback |
| `OnExit(Action)` | `State<T>` | Set exit callback |
| `OnTimeout(Action)` | `State<T>` | Set timeout callback (normal) |
| `OnTimeoutExpired(Action)` | `State<T>` | Set timeout callback (when locked) |

#### Transitions
| Method | Returns | Description |
|--------|---------|-------------|
| `AddTransition(T to)` | `Transition<T>` | Add transition to another state |
| `RemoveTransition(T to)` | `bool` | Remove transition |

#### Timing
| Method | Returns | Description |
|--------|---------|-------------|
| `MinDuration(float)` | `State<T>` | Set minimum state duration |
| `TimeoutAfter(float)` | `State<T>` | Set timeout duration |
| `TimeoutAfter(float, T to)` | `State<T>` | Set timeout with target state |

#### Configuration
| Method | Returns | Description |
|--------|---------|-------------|
| `SetProcessMode(FSMProcessMode)` | `State<T>` | Set Idle or Fixed update mode |
| `Lock(FSMLockMode)` | `State<T>` | Lock state (Full or Transition) |
| `Unlock()` | `State<T>` | Unlock state |
| `SetCooldown(float)` | `State<T>` | Set state cooldown duration |
| `ApplyTemplate(StateTemplate<T>)` | `State<T>` | Apply template configuration |

#### Tags & Data
| Method | Returns | Description |
|--------|---------|-------------|
| `AddTags(params string[])` | `State<T>` | Add tags to state |
| `HasTag(string)` | `bool` | Check if has tag |
| `SetData(string, object)` | `State<T>` | Store state data |
| `TryGetData<TData>(string, out TData)` | `bool` | Retrieve state data |
| `RemoveData(string)` | `bool` | Remove data entry |
| `HasData(string)` | `bool` | Check if data key exists |
| `HasData(object)` | `bool` | Check if data value exists |

#### State Checks
| Method | Returns | Description |
|--------|---------|-------------|
| `IsLocked()` | `bool` | Check if any lock active |
| `IsFullyLocked()` | `bool` | Check if fully locked |
| `TransitionBlocked()` | `bool` | Check if transition-locked |
| `IsOnCooldown()` | `bool` | Check cooldown status |

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `Id` | `T` | State identifier |
| `TimeoutTargetId` | `T` | Target state for timeout |
| `Transitions` | `List<Transition<T>>` | All transitions |
| `MinTime` | `float` | Minimum duration |
| `Timeout` | `float` | Timeout duration |
| `ProcessMode` | `FSMProcessMode` | Update mode |
| `LockMode` | `FSMLockMode` | Lock mode |
| `Cooldown` | `Cooldown` | Cooldown object |
| `Tags` | `IReadOnlyCollection<string>` | State tags |
| `Data` | `IReadOnlyDictionary<string, object>` | State data |

---

### Transition\<T\> Methods

#### Configuration
| Method | Returns | Description |
|--------|---------|-------------|
| `SetCondition(Predicate<StateMachine<T>>)` | `Transition<T>` | Set transition condition |
| `SetGuard(Predicate<StateMachine<T>>)` | `Transition<T>` | Set guard (pre-check) |
| `OnEvent(string)` | `Transition<T>` | Trigger on event name |
| `OnEvent<TEvent>(TEvent)` | `Transition<T>` | Trigger on enum event |
| `OnTrigger(Action)` | `Transition<T>` | Set trigger callback |
| `SetPriority(int)` | `Transition<T>` | Set evaluation priority |
| `HighestPriority()` | `Transition<T>` | Set to max priority |
| `RequireMinTime(float)` | `Transition<T>` | Override state min time |
| `ForceInstant()` | `Transition<T>` | Bypass time requirements |
| `BreakInstant()` | `Transition<T>` | Disable instant mode |
| `SetCooldown(float)` | `Transition<T>` | Set cooldown duration |

#### State Checks
| Method | Returns | Description |
|--------|---------|-------------|
| `IsOnCooldown()` | `bool` | Check cooldown status |

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `From` | `T` | Source state |
| `To` | `T` | Target state |
| `Guard` | `Predicate<StateMachine<T>>` | Pre-condition |
| `Condition` | `Predicate<StateMachine<T>>` | Transition logic |
| `OnTriggered` | `Action` | Trigger callback |
| `EventName` | `string` | Event trigger name |
| `OverrideMinTime` | `float` | Min time override |
| `Priority` | `int` | Evaluation priority |
| `ForceInstantTransition` | `bool` | Instant flag |
| `Cooldown` | `Cooldown` | Cooldown object |

---

### StateHistory\<T\> Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `SetActive(bool)` | `void` | Enable/disable tracking |
| `SetCapacity(int)` | `void` | Set max history size |
| `GetHistory()` | `IReadOnlyList<HistoryEntry<T>>` | Get full history (recent first) |
| `GetRecentHistory(int count)` | `List<HistoryEntry<T>>` | Get N recent entries |
| `GetEntry(int index)` | `HistoryEntry<T>` | Get specific entry |
| `RemoveRange(int start, int count)` | `void` | Remove entry range |
| `ClearHistory()` | `void` | Clear all history |

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `IsActive` | `bool` | Is tracking enabled |
| `Capacity` | `int` | Max entries |
| `CurrentSize` | `int` | Current entry count |

---

### StateTemplate\<T\> Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `WithUpdate(Action<float>)` | `StateTemplate<T>` | Set update callback |
| `WithEnter(Action)` | `StateTemplate<T>` | Set enter callback |
| `WithExit(Action)` | `StateTemplate<T>` | Set exit callback |
| `WithMinDuration(float)` | `StateTemplate<T>` | Set min duration |
| `WithTimeout(float, T)` | `StateTemplate<T>` | Set timeout |
| `WithLock(FSMLockMode)` | `StateTemplate<T>` | Set lock mode |
| `WithProcessMode(FSMProcessMode)` | `StateTemplate<T>` | Set process mode |
| `WithCooldown(float)` | `StateTemplate<T>` | Set cooldown |
| `WithTags(params string[])` | `StateTemplate<T>` | Add tags |
| `WithData(string, object)` | `StateTemplate<T>` | Set data |
| `ApplyTo(State<T>)` | `void` | Apply to state |

---

### Cooldown Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `SetDuration(float)` | `void` | Set cooldown duration |
| `Update(float delta)` | `void` | Update timer (internal) |
| `Start()` | `void` | Start cooldown (internal) |
| `Reset()` | `void` | Reset cooldown |
| `GetRemaining()` | `float` | Get remaining time |
| `GetProgress()` | `float` | Get progress 0.0-1.0 |
| `GetNormalizedRemaining()` | `float` | Get remaining normalized 0.0-1.0 |
| `IsComplete()` | `bool` | Check if complete |

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `Duration` | `float` | Cooldown duration |
| `IsActive` | `bool` | Is cooldown active |

---

### Enums

#### FSMProcessMode
- `Idle` - Update in _Process
- `Fixed` - Update in _PhysicsProcess

#### FSMLockMode
- `None` - Not locked
- `Full` - Completely locked (only timeout can transition)
- `Transition` - Transition-locked (only timeout can transition)

---

### Helper Structures

#### HistoryEntry\<T\>
```csharp
public struct HistoryEntry<T> where T : Enum
{
    public T StateId { get; }          // State that was active
    public float TimeSpent { get; }     // How long in this state
    public float TimeStamp { get; }     // When it occurred
}
```

---

### ILogger Interface

Implement custom logging:

```csharp
public interface ILogger
{
    void LogError(string text);
    void LogWarning(string text);
}

// Example: Godot logger
public class GodotLogger : ILogger
{
    public void LogError(string text) => GD.PrintErr($"[FSM] {text}");
    public void LogWarning(string text) => GD.Print($"[FSM WARNING] {text}");
}

var fsm = new StateMachine<PlayerState>(new GodotLogger());
```

## 📜 License

This is a custom FSM library for Godot. Feel free to use and modify for your projects!

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!

## ⭐ Credits

Built for Godot 4 C# game development.
