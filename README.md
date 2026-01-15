# 🎮 Stately

> Elegant State Machine for Godot C#

[![License: MIT](https://img.shields.io/badge/license-Custom-blue.svg)](LICENSE)
[![Godot 4+](https://img.shields.io/badge/Godot-4%2B-blue)](https://godotengine.org/)

A powerful, type-safe finite state machine library with full support for 
hierarchical states, smart transitions, and comprehensive manual control.

## ✨ Features

- **Type-Safe**: Generic implementation using C# enums for state IDs
- **Hierarchical States**: Full support for nested states with unlimited depth (NEW!)
- **Smart Transitions**: Automatic child resolution when transitioning to parent states (NEW!)
- **Type-Based Data Storage**: No boxing for reference types, cleaner API (IMPROVED!)
- **Comprehensive Manual Control**: Force transitions, validate before transitioning, and more (IMPROVED!)
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
        fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
            .SetCondition(sm => Input.IsActionPressed("move_right"));

        // Set initial state and start
        fsm.SetInitialId(PlayerState.Idle);
        fsm.Start();
    }

    public override void _Process(double delta)
    {
        fsm.UpdateIdle((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        fsm.UpdateFixed((float)delta);
    }
}
```

## 🆕 Hierarchical States (Nested States)

Create parent-child state relationships for complex behavior organization:

### Basic Nested States

```csharp
enum GameState
{
    // Root states
    Combat,
    Movement,
    
    // Combat children
    CombatIdle,
    CombatAttacking,
    CombatDefending,
    
    // Attacking sub-children
    AttackingLight,
    AttackingHeavy,
    
    // Movement children
    MovementWalking,
    MovementRunning
}

// Create parent states
var combat = fsm.AddState(GameState.Combat)
    .OnEnter(() => GD.Print("Entered Combat Mode"));

// Add children to parent
var combatIdle = fsm.AddChildState(GameState.Combat, GameState.CombatIdle)
    .OnEnter(() => GD.Print("Ready to fight"))
    .OnUpdate(delta => IdleAnimation());

var combatAttacking = fsm.AddChildState(GameState.Combat, GameState.CombatAttacking)
    .OnEnter(() => GD.Print("Attacking!"));

// Set default child (auto-set to first child if not specified)
combat.SetDefaultChild(GameState.CombatIdle);

// Deep nesting: Create sub-children
var attackingLight = fsm.AddChildState(GameState.CombatAttacking, GameState.AttackingLight);
var attackingHeavy = fsm.AddChildState(GameState.CombatAttacking, GameState.AttackingHeavy);
combatAttacking.SetDefaultChild(GameState.AttackingLight);

// Start at Movement (will auto-enter default child)
fsm.SetInitialId(GameState.Movement);
fsm.Start();
```

### Smart Transitions

Transitions automatically resolve parent states to their default children:

```csharp
// Transition to parent - automatically enters Combat.CombatIdle
fsm.AddTransition(GameState.MovementWalking, GameState.Combat)
    .SetCondition(fsm => EnemyNearby());

// Or transition directly to a specific child
fsm.AddTransition(GameState.MovementWalking, GameState.CombatAttacking)
    .SetCondition(fsm => AttackPressed());

// Sibling transitions stay within parent (efficient)
fsm.AddTransition(GameState.CombatIdle, GameState.CombatAttacking)
    .SetCondition(fsm => AttackPressed());
// Only calls: Idle.Exit() → Attacking.Enter()
// Combat stays active!

// Cross-parent transitions
fsm.AddTransition(GameState.CombatIdle, GameState.MovementWalking)
    .SetCondition(fsm => NoEnemies());
// Calls: Idle.Exit() → Combat.Exit() → Movement.Enter() → Walking.Enter()
```

### Hierarchical Queries

```csharp
// Get active hierarchy (root to leaf)
var hierarchy = fsm.GetActiveHierarchy();
// Result: [Combat, Attacking, Light]

// Check if any state in hierarchy is active
if (fsm.IsInHierarchy(GameState.Combat))
    GD.Print("In combat mode!");

// Get active child of a parent
var activeChild = fsm.GetActiveChild(GameState.Combat);
// Returns: CombatIdle or CombatAttacking or CombatDefending

// Get all children of a parent
var children = fsm.GetChildren(GameState.Combat);
foreach (var child in children)
    GD.Print($"Child: {child.Id}");

// Get all ancestors (parents, grandparents, etc.)
var ancestors = fsm.GetAncestors(GameState.AttackingLight);
// Result: [CombatAttacking, Combat]

// Get hierarchy path for any state
var path = fsm.GetHierarchyPath(GameState.AttackingLight);
// Result: [Combat, CombatAttacking, AttackingLight]

// Check state type
bool isParent = fsm.IsParentState(GameState.Combat);     // true
bool isChild = fsm.IsChildState(GameState.CombatIdle);   // true
bool isLeaf = fsm.IsLeafState(GameState.CombatIdle);     // true

// Get depth (nesting level)
int depth = fsm.GetDepth(GameState.AttackingLight);      // 2

// Get root state
var root = fsm.GetRootState(GameState.AttackingLight);   // Combat
```

### Validation

```csharp
// Validate hierarchy setup
if (!fsm.ValidateHierarchy(out var errors))
{
    foreach (var error in errors)
        GD.PrintErr($"FSM Error: {error}");
}
// Checks:
// - All parents have default children
// - No circular references
// - All parent/child references are valid
```

### Key Hierarchical Behaviors

1. **Entry/Exit**: When entering a state, all ancestors' Enter() is called from root to leaf. When exiting, all Exit() is called from leaf to root.

2. **Update**: Only the deepest active child (leaf) Update() is called each frame.

3. **Sibling Transitions**: When transitioning between children of the same parent, the parent stays active (no Exit/Enter on parent).

4. **Smart Resolution**: Transitioning to a parent automatically enters its default child (recursively resolved to leaf).

5. **History**: History tracks the full leaf state, not the entire hierarchy.

## 🆕 Improved Data Storage (Type-Based)

Store data without string keys and eliminate boxing for reference types:

### Global Data

```csharp
// Old way (string keys, boxing for value types)
fsm.SetData("player_health", 100);
fsm.TryGetData<int>("player_health", out var health);

// NEW: Type-based (no string keys, no boxing for classes)
public class PlayerData 
{
    public int Health;
    public float Stamina;
}

fsm.SetData(new PlayerData { Health = 100, Stamina = 50 });

if (fsm.TryGetData<PlayerData>(out var playerData))
{
    GD.Print($"Health: {playerData.Health}");
}

// Or use direct getter
var data = fsm.GetData<PlayerData>();
if (data != null)
{
    data.Health -= 10;
}

// Remove data
fsm.RemoveData<PlayerData>();
```

### State-Specific Data

```csharp
public class AttackData
{
    public int ComboCount;
    public float DamageMultiplier;
}

// Set data on state
fsm.GetState(GameState.Attacking)
    .SetData(new AttackData { ComboCount = 0, DamageMultiplier = 1.5f });

// Retrieve data
var attackState = fsm.CurrentState;
if (attackState.TryGetData<AttackData>(out var attackData))
{
    attackData.ComboCount++;
}

// Or use direct getter
var data = attackState.GetData<AttackData>();

// Remove data
attackState.RemoveData<AttackData>();

// Check if data exists
if (attackState.HasData<AttackData>())
    GD.Print("Has attack data");
```

### Transition Data

```csharp
public class DamageInfo
{
    public int Amount;
    public string Type;
    
    public DamageInfo(int amount, string type)
    {
        Amount = amount;
        Type = type;
    }
}

// Pass data during transition
fsm.TryTransitionTo(GameState.Damaged, new DamageInfo(25, "fire"));

// Retrieve in new state's Enter callback
fsm.AddState(GameState.Damaged)
    .OnEnter(() =>
    {
        if (fsm.TryGetTransitionData<DamageInfo>(out var damage))
        {
            GD.Print($"Took {damage.Amount} {damage.Type} damage");
            ApplyDamage(damage);
        }
    });
```

**Benefits:**
- ✅ No string keys to manage
- ✅ Type-safe (compiler catches errors)
- ✅ No boxing for reference types
- ✅ IntelliSense/autocomplete works
- ✅ Forces better code organization

**Limitation:**
- Can only store ONE instance per type
- Solution: Create wrapper classes for multiple values of same type

```csharp
// If you need multiple ints, create a wrapper
public class CombatStats
{
    public int Health;
    public int Mana;
    public int Stamina;
}

state.SetData(new CombatStats { Health = 100, Mana = 50, Stamina = 75 });
```

## 🆕 Improved Manual Control

New and improved methods for direct FSM manipulation:

### Validation Before Transition

```csharp
// Check if transition is possible
if (fsm.CanTransitionTo(PlayerState.Attacking))
{
    fsm.TryTransitionTo(PlayerState.Attacking);
}

// Get detailed reason why transition failed
if (!fsm.CanTransitionTo(PlayerState.Attacking, out string reason))
{
    GD.Print($"Cannot attack: {reason}");
    // Possible reasons:
    // - "Current state is locked"
    // - "Minimum time not exceeded"
    // - "Target state does not exist"
    // - "Target state is on cooldown"
    // - "Parent state has no default child"
}
```

### Force Transitions

```csharp
// Try transition (respects locks, cooldowns, min time)
bool success = fsm.TryTransitionTo(PlayerState.Attacking);

// Force transition (bypasses ALL restrictions except state existence)
fsm.ForceTransitionTo(PlayerState.GameOver);
// Use for: cutscenes, game over, debug commands
```

### Transition with Data

```csharp
// Transition and pass data
fsm.TryTransitionTo(PlayerState.Damaged, new DamageInfo(10, "fire"));

// Check before transitioning
if (fsm.CanTransitionTo(PlayerState.Shop))
{
    fsm.TryTransitionTo(PlayerState.Shop, new ShopData { Gold = playerGold });
}
```

### Manual Timeout Trigger

```csharp
// Manually trigger the current state's timeout
if (fsm.TriggerTimeout())
    GD.Print("Timeout triggered");
```

### State Time Manipulation

```csharp
// Get current state time
float timeInState = fsm.StateTime;  // Also: fsm.GetStateTime()

// Reset state timer (keeps current state)
fsm.ResetStateTime();

// Set state time (e.g., to skip ahead)
fsm.SetStateTime(5.0f);

// Add time
fsm.AddStateTime(2.0f);
```

### Get Valid Transitions

```csharp
// Get list of states we can currently transition to
var validTransitions = fsm.GetValidTransitions();
foreach (var targetState in validTransitions)
{
    GD.Print($"Can go to: {targetState}");
    // Only includes transitions that pass CanTransitionTo() check
}

// Useful for UI (showing available actions/abilities)
```

### Lifecycle Control

```csharp
// Start FSM
fsm.Start();

// Stop FSM (calls Exit on current state, resets everything)
fsm.Stop();

// Pause/Resume
fsm.Pause();
fsm.Resume();
fsm.TogglePaused(true);

// Check if active
if (fsm.IsActive())
    GD.Print("FSM is running");

// Reset to initial state
fsm.Reset();
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

### States

States represent distinct behavioral modes. Each state can have:
- **Enter/Exit callbacks**: Called when entering or leaving the state
- **Update callback**: Called every frame while in the state (only for leaf states)
- **Minimum duration**: Prevent premature transitions
- **Timeout**: Automatically transition after a duration
- **Lock mode**: Prevent transitions
- **Cooldown**: Prevent re-entry for a duration
- **Tags**: Organize states by category
- **Data**: Store state-specific typed data
- **Parent/Children**: Hierarchical relationships (NEW!)

```csharp
fsm.AddState(PlayerState.Attacking)
    .OnEnter(() => PlayAttackAnimation())
    .OnUpdate(delta => UpdateAttack(delta))
    .OnExit(() => ResetAttack())
    .MinDuration(0.3f)              // Must stay at least 0.3s
    .TimeoutAfter(1.0f, PlayerState.Idle)  // Auto-return to Idle after 1s
    .SetCooldown(2.0f)              // Can't attack again for 2s
    .AddTags("combat", "active")
    .SetData(new AttackData { Damage = 10 });
```

### Transitions

Transitions define how to move between states. They support:
- **Conditions**: Logic that must be true to transition
- **Guards**: Pre-checks evaluated before time requirements
- **Events**: Trigger on named events
- **Priority**: Control evaluation order
- **Cooldowns**: Prevent rapid re-triggering
- **Minimum time override**: Per-transition timing requirements
- **Smart resolution**: Auto-resolve parent states to children (NEW!)

```csharp
// Condition-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
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

**Note:** In hierarchical states, only the leaf (deepest active child) updates. Parent states' Update() is not called.

### Bulk Transition Configuration

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

// Reset transition (return to initial state)
fsm.AddResetTransition(PlayerState.GameOver);

// Self transition (restart same state)
fsm.AddSelfTransition(PlayerState.Attacking)
   .SetCondition(sm => CanCombo())
   .OnTrigger(() => ResetCombo());
```

### State Locking

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

// Or access via current state
fsm.CurrentState.Lock();
fsm.CurrentState.Unlock();
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

Track and navigate through previous states:

```csharp
// Go back to previous state
if (fsm.CanGoBack())
    fsm.GoBack();

// Go back multiple states
if (fsm.CanGoBack(3))
    fsm.GoBack(3);

// Go back to specific state
fsm.GoBackToState(PlayerState.Idle);

// Peek without transitioning
var prevState = fsm.PeekBackState();
var older = fsm.PeekBackState(3);

// Find state in history
int stepsBack = fsm.FindInHistory(PlayerState.Idle);

// Access full history
var history = fsm.StateHistory.GetHistory();
foreach (var entry in history)
{
    GD.Print($"{entry.StateId}: {entry.TimeSpent:F2}s at {entry.TimeStamp:F2}s");
}

// Configure history
fsm.SetHistoryActive(true/false);
fsm.StateHistory.SetCapacity(50);
fsm.StateHistory.ClearHistory();
```

**Note:** For hierarchical states, history tracks only the leaf state, not the entire hierarchy.

### Events

Use events for complex state changes:

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

// Subscribe to FSM events
fsm.StateChanged += (from, to) => GD.Print($"Changed: {from} -> {to}");
fsm.TransitionTriggered += (from, to) => GD.Print($"Transition: {from} -> {to}");

// Custom event listeners
fsm.OnEvent("player_hit", () => {
    GD.Print("Player was hit!");
    FlashSprite();
});
```

### State Templates

Reuse common state configurations:

```csharp
// Create a template for combat states
var combatTemplate = new StateTemplate<PlayerState>()
    .WithEnter(() => StartCombat())
    .WithExit(() => EndCombat())
    .WithTags("combat")
    .WithLock(FSMLockMode.Transition)
    .WithMinDuration(0.2f)
    .WithData(new CombatData { InCombat = true });

// Apply to multiple states
fsm.AddState(PlayerState.Attacking).ApplyTemplate(combatTemplate);
fsm.AddState(PlayerState.Blocking).ApplyTemplate(combatTemplate);
```

### Cooldowns

Prevent rapid state/transition re-triggering:

```csharp
// State cooldown
fsm.AddState(PlayerState.Dashing)
    .SetCooldown(3.0f);

if (fsm.IsStateOnCooldown(PlayerState.Dashing))
{
    var state = fsm.GetState(PlayerState.Dashing);
    float remaining = state.Cooldown.GetRemaining();
    GD.Print($"Cooldown: {remaining:F1}s");
}

// Transition cooldown
fsm.AddTransition(PlayerState.Idle, PlayerState.Attacking)
    .SetCooldown(1.0f);

// Reset cooldowns
fsm.ResetStateCooldown(PlayerState.Dashing);
fsm.ResetTransitionCooldown(PlayerState.Idle, PlayerState.Attacking);
fsm.ResetAllCooldowns();

// Configure cooldown update mode
fsm.SetCooldownTimersProcessMode(FSMProcessMode.Idle);  // or Fixed
```

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

### State Queries

```csharp
// Current state
if (fsm.IsCurrentState(PlayerState.Attacking))
    GD.Print("Currently attacking!");

var currentId = fsm.GetCurrentId();
var currentState = fsm.CurrentState;

// Previous state
if (fsm.IsPreviousState(PlayerState.Jumping))
    GD.Print("Just landed!");

// Tag queries
if (fsm.IsInStateWithTag("airborne"))
    GD.Print("Player is in the air!");

var combatState = fsm.GetStateWithTag("combat");
var groundedStates = fsm.GetStatesWithTag("grounded");

// Timing
float timeInState = fsm.StateTime;
float minTime = fsm.GetMinStateTime();
bool canTransition = fsm.MinTimeExceeded();

// Timeout progress
float progress = fsm.GetTimeoutProgress();  // 0.0 to 1.0
float remaining = fsm.GetRemainingTime();
```

## 📋 Best Practices

1. **Use Enums**: Always use enums for state IDs - provides type safety
2. **Hierarchical Organization**: Group related states under parent states
3. **Type-Safe Data**: Create data classes instead of using primitives
4. **Tag Your States**: Use tags to group related states
5. **Guards vs Conditions**: Use guards for prerequisites, conditions for triggers
6. **Lock Important States**: Prevent interruption of critical animations
7. **Set Cooldowns**: Prevent spamming of abilities
8. **Use Templates**: Share configurations across similar states
9. **Track History**: Enable history for undo systems or debugging
10. **Validate Hierarchy**: Call `ValidateHierarchy()` after setup

## 🐛 Debugging Tips

```csharp
// Log all state changes
fsm.StateChanged += (from, to) => GD.Print($"{from} -> {to}");

// Check active hierarchy
var hierarchy = fsm.GetActiveHierarchy();
GD.Print($"Active: {string.Join(" > ", hierarchy)}");

// View available transitions
var transitions = fsm.GetValidTransitions();
foreach (var t in transitions)
    GD.Print($"Can go to: {t}");

// View state history
var history = fsm.StateHistory.GetRecentHistory(10);
foreach (var entry in history)
    GD.Print($"[{entry.TimeStamp:F2}s] {entry.StateId} ({entry.TimeSpent:F2}s)");

// Validate hierarchy
if (!fsm.ValidateHierarchy(out var errors))
{
    foreach (var error in errors)
        GD.PrintErr(error);
}

// Check cooldowns
int activeCooldowns = fsm.GetActiveCooldownCount();
GD.Print($"Active cooldowns: {activeCooldowns}");
```

## 📜 License

Custom License - See [LICENSE](LICENSE) file for full terms.

### TL;DR - You can:
✅ Use this FSM freely in any project (personal or commercial)
✅ Modify it for your needs
✅ Ship it in your games without crediting in-game

### You must:
⚠️ Give credit if you **advertise** that your game uses this FSM
⚠️ Not claim it as your own creation

### Example:
❌ NO credit needed: Just using the FSM in your game
✅ Credit needed: "Made with [FSM Name]" in your Steam page/trailer
✅ Credit format: "Uses [FSM Name] by [Your Name]" or link to GitHub

---

**Simple rule:** Use freely, credit if you promote it. Don't steal. ✌️
```

---

## 🎯 **Real-World Examples**

### ✅ **No Credit Required:**
```
User makes a game using your FSM
→ Releases it on Steam
→ Game description: "A fast-paced action game..."
→ NO credit needed (not mentioning the FSM)
```

### ✅ **Credit Required:**
```
User makes a game using your FSM
→ Releases it on Steam
→ Game description: "Built with a powerful FSM system..."
→ MUST credit: "Uses [FSM Name] by [Your Name]"
```

### ✅ **Credit Required:**
```
User makes a YouTube tutorial
→ "How to build an AI using this FSM library"
→ MUST credit in video description
```

### ❌ **Prohibited:**
```
User forks your repo
→ Renames it to "SuperFSM"
→ Claims they created it
→ VIOLATION

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!

## ⭐ Credits

Built for Godot 4 C# game development.
