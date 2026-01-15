# 🎮 Stately

> **Powerful, Type-Safe Finite State Machine for Godot C#**

[![License: MIT](https://img.shields.io/badge/license-Custom-blue.svg)](LICENSE)
[![Godot 4+](https://img.shields.io/badge/Godot-4%2B-478cbf?logo=godot-engine&logoColor=white)](https://godotengine.org/)
[![C#](https://img.shields.io/badge/C%23-.NET-512BD4?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)

A production-ready finite state machine library designed for complex game logic, featuring hierarchical states, type-safe data storage, and comprehensive manual control.

---

## 📋 Table of Contents

- [Features](#-features)
- [Installation](#-installation)
- [Quick Start](#-quick-start)
- [Core Concepts](#-core-concepts)
- [Hierarchical States](#-hierarchical-states-new)
- [Type-Based Data Storage](#-type-based-data-storage-improved)
- [Manual Control](#-manual-control-improved)
- [Advanced Features](#-advanced-features)
- [Best Practices](#-best-practices)
- [Examples](#-examples)
- [License](#-license)

---

## ✨ Features

### 🆕 New in Latest Version
- **Hierarchical States**: Unlimited nesting depth with parent-child relationships
- **Smart Transitions**: Automatic child resolution when transitioning to parent states
- **Type-Based Data Storage**: No boxing for reference types, cleaner API
- **Enhanced Manual Control**: Force transitions, validate before transitioning, and more

### Core Features
- ✅ **Type-Safe**: Generic implementation using C# enums for state IDs
- ✅ **Flexible Transitions**: Condition-based, event-driven, or time-based
- ✅ **Guards & Conditions**: Pre-validation and transition logic separation
- ✅ **Cooldowns**: Built-in cooldown system for states and transitions
- ✅ **State Locking**: Prevent unwanted transitions with flexible lock modes
- ✅ **State History**: Navigate backward through state history
- ✅ **Global Transitions**: Define transitions that work from any state
- ✅ **State Templates**: Reusable state configurations
- ✅ **Timeout System**: Automatic state timeouts with custom handlers
- ✅ **Event System**: Event-driven transitions and listeners
- ✅ **Process Modes**: Support for `_Process` (Idle) and `_PhysicsProcess` (Fixed) updates
- ✅ **Priority System**: Control transition evaluation order
- ✅ **Tags**: Organize and query states using tags

---

## 📦 Installation

1. Download or clone this repository
2. Copy all `.cs` files into your Godot C# project (e.g., `res://Scripts/StateMachine/`)
3. Ensure your project uses **.NET** and **C# scripting**
4. Import the namespace: `using Stately;`

**Requirements:**
- .NET 6.0+
- C# 8.0+

---

## 🚀 Quick Start

### Basic Example

```csharp
using Godot;
using Stately;

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
        // Create state machine
        fsm = new StateMachine<PlayerState>();

        // Add states with callbacks
        fsm.AddState(PlayerState.Idle)
            .OnEnter(() => GD.Print("Entered Idle"))
            .OnUpdate(delta => CheckInput(delta))
            .OnExit(() => GD.Print("Exited Idle"));

        fsm.AddState(PlayerState.Walking)
            .OnEnter(() => PlayWalkAnimation());

        // Add transition with condition
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

---

## 📖 Core Concepts

### States

States represent distinct behavioral modes with lifecycle callbacks:

```csharp
fsm.AddState(PlayerState.Attacking)
    .OnEnter(() => PlayAttackAnimation())
    .OnUpdate(delta => UpdateAttack(delta))
    .OnExit(() => ResetAttack())
    .MinDuration(0.3f)                          // Must stay at least 0.3s
    .TimeoutAfter(1.0f, PlayerState.Idle)       // Auto-return to Idle after 1s
    .SetCooldown(2.0f)                          // Can't attack again for 2s
    .AddTags("combat", "active")
    .SetData(new AttackData { Damage = 10 });
```

**State Features:**
- `OnEnter()` - Called when entering the state
- `OnUpdate(delta)` - Called every frame while active
- `OnExit()` - Called when leaving the state
- `MinDuration()` - Minimum time before allowing transitions
- `TimeoutAfter()` - Automatic transition after duration
- `SetCooldown()` - Cooldown before re-entry
- `AddTags()` - Tag states for organization
- `SetData<T>()` - Store typed data on the state

### Transitions

Transitions define how to move between states:

```csharp
// Condition-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
    .SetCondition(sm => velocity.Length() > 0);

// Event-based transition
fsm.AddTransition(PlayerState.Idle, PlayerState.Jumping)
    .OnEvent("jump");

// Guarded transition with priority
fsm.AddTransition(PlayerState.Walking, PlayerState.Attacking)
    .SetGuard(sm => HasWeapon())                // Checked FIRST
    .SetCondition(sm => AttackPressed())        // Then this
    .SetPriority(10)                            // Higher = checked first
    .SetCooldown(0.5f);                         // 500ms cooldown
```

**Guard vs Condition:**
- **Guard**: Pre-validation (e.g., "Has ammo?") - checked FIRST
- **Condition**: Actual trigger (e.g., "Fire pressed?") - checked AFTER guard and time requirements

### Configuration Methods

Choose your preferred style:

**Method 1: Inline Chaining**
```csharp
fsm.AddState(PlayerState.Idle)
    .OnEnter(() => PlayIdleAnimation())
    .AddTransition(PlayerState.Walking)
        .SetCondition(sm => IsMoving());
```

**Method 2: Configuration Lambdas**
```csharp
// Configure state - auto-creates if doesn't exist
fsm.ConfigState(PlayerState.Idle, state => {
    state.OnEnter(() => PlayIdleAnimation())
         .MinDuration(0.1f)
         .AddTags("grounded");
});

// Configure transition - auto-creates if doesn't exist
fsm.ConfigTransition(PlayerState.Idle, PlayerState.Walking, transition => {
    transition.SetCondition(sm => IsMoving())
              .SetPriority(5);
});
```

---

## 🆕 Hierarchical States (NEW)

Organize complex behaviors with parent-child state relationships:

### Creating Nested States

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
    .OnEnter(() => GD.Print("Ready to fight"));

var combatAttacking = fsm.AddChildState(GameState.Combat, GameState.CombatAttacking);

// Set default child (auto-enters when parent is entered)
combat.SetDefaultChild(GameState.CombatIdle);

// Deep nesting: Create sub-children
var attackingLight = fsm.AddChildState(GameState.CombatAttacking, GameState.AttackingLight);
combatAttacking.SetDefaultChild(GameState.AttackingLight);
```

### Smart Transitions

Transitions automatically resolve parent states to their default children:

```csharp
// Transition to parent → automatically enters Combat.CombatIdle
fsm.AddTransition(GameState.MovementWalking, GameState.Combat)
    .SetCondition(fsm => EnemyNearby());

// Sibling transitions (efficient - parent stays active)
fsm.AddTransition(GameState.CombatIdle, GameState.CombatAttacking)
    .SetCondition(fsm => AttackPressed());
// Only calls: Idle.Exit() → Attacking.Enter()
// Combat remains active!

// Cross-parent transitions (full hierarchy change)
fsm.AddTransition(GameState.CombatIdle, GameState.MovementWalking)
    .SetCondition(fsm => NoEnemies());
// Calls: Idle.Exit() → Combat.Exit() → Movement.Enter() → Walking.Enter()
```

### Hierarchical Queries

```csharp
// Get active hierarchy (root to leaf)
var hierarchy = fsm.GetActiveHierarchy();
// Result: [Combat, Attacking, Light]

// Check if in hierarchy
if (fsm.IsInHierarchy(GameState.Combat))
    GD.Print("In combat mode!");

// Get active child of a parent
var activeChild = fsm.GetActiveChild(GameState.Combat);

// Get all children
var children = fsm.GetChildren(GameState.Combat);

// Get ancestors (parents, grandparents, etc.)
var ancestors = fsm.GetAncestors(GameState.AttackingLight);
// Result: [CombatAttacking, Combat]

// Get full hierarchy path for any state
var path = fsm.GetHeirarchyPath(GameState.AttackingLight);
// Result: [Combat, CombatAttacking, AttackingLight]

// Check state type
bool isParent = fsm.IsParentState(GameState.Combat);      // true
bool isLeaf = fsm.IsLeafState(GameState.CombatIdle);      // true

// Get depth (0 = root, 1 = child, etc.)
int depth = fsm.GetDepth(GameState.AttackingLight);       // 2

// Get root state
var root = fsm.GetRootState(GameState.AttackingLight);    // Combat
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
// ✓ All parents have default children
// ✓ No circular references
// ✓ All parent/child references are valid
```

### Key Behaviors

1. **Entry/Exit**: Ancestors' `Enter()` called root→leaf, `Exit()` called leaf→root
2. **Update**: Only the deepest active child (leaf) `Update()` is called
3. **Sibling Transitions**: Parent stays active (no Exit/Enter on parent)
4. **Smart Resolution**: Parent states auto-resolve to default children
5. **History**: Tracks only the leaf state, not entire hierarchy

---

## 🆕 Type-Based Data Storage (IMPROVED)

Store data without string keys and eliminate boxing:

### Global Data

```csharp
public class PlayerData 
{
    public int Health;
    public float Stamina;
}

// Set data (no boxing for reference types)
fsm.SetData(new PlayerData { Health = 100, Stamina = 50 });

// Get data
if (fsm.TryGetData<PlayerData>(out var playerData))
{
    GD.Print($"Health: {playerData.Health}");
}

// Or direct getter
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

// Retrieve in callbacks
var attackState = fsm.CurrentState;
if (attackState.TryGetData<AttackData>(out var attackData))
{
    attackData.ComboCount++;
}
```

### Transition Data

```csharp
public class DamageInfo
{
    public int Amount { get; set; }
    public string Type { get; set; }
}

// Pass data during transition
fsm.TryTransitionTo(GameState.Damaged, new DamageInfo 
{ 
    Amount = 25, 
    Type = "fire" 
});

// Retrieve in new state's Enter callback
fsm.AddState(GameState.Damaged)
    .OnEnter(() =>
    {
        if (fsm.TryGetTransitionData<DamageInfo>(out var damage))
        {
            GD.Print($"Took {damage.Amount} {damage.Type} damage");
        }
    });
```

**Benefits:**
- ✅ No string keys to manage
- ✅ Type-safe (compiler catches errors)
- ✅ No boxing for reference types
- ✅ IntelliSense/autocomplete works
- ✅ Forces better code organization

**Note:** Can only store ONE instance per type. Use wrapper classes for multiple values:

```csharp
public class CombatStats
{
    public int Health;
    public int Mana;
    public int Stamina;
}

state.SetData(new CombatStats { Health = 100, Mana = 50, Stamina = 75 });
```

---

## 🆕 Manual Control (IMPROVED)

New methods for direct FSM manipulation:

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

// Force transition (bypasses ALL restrictions)
fsm.ForceTransitionTo(PlayerState.GameOver);
// Use cases: cutscenes, game over, debug commands
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

### Manual Timeout & Time Control

```csharp
// Manually trigger timeout
if (fsm.TriggerTimeout())
    GD.Print("Timeout triggered");

// Get/manipulate state time
float timeInState = fsm.StateTime;
fsm.ResetStateTime();
fsm.SetStateTime(5.0f);
fsm.AddStateTime(2.0f);
```

### Get Valid Transitions

```csharp
// Get list of currently valid transitions
var validTransitions = fsm.GetValidTransitions();
foreach (var targetState in validTransitions)
{
    GD.Print($"Can go to: {targetState}");
}
// Useful for UI (showing available actions/abilities)
```

### Lifecycle Control

```csharp
// Start/Stop
fsm.Start();
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

---

## 🛠️ Advanced Features

### Process Modes

Control when state updates occur:

```csharp
// Update in _Process (default)
fsm.AddState(PlayerState.Idle)
    .SetProcessMode(FSMProcessMode.Idle);

// Update in _PhysicsProcess
fsm.AddState(PlayerState.Falling)
    .SetProcessMode(FSMProcessMode.Fixed);

// In your node:
public override void _Process(double delta)
{
    fsm.UpdateIdle((float)delta);
}

public override void _PhysicsProcess(double delta)
{
    fsm.UpdateFixed((float)delta);
}
```

### State Locking

Prevent unwanted transitions:

```csharp
// Full lock: No transitions, timeout triggers TimeoutExpired
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

Transitions that work from any state:

```csharp
// Can transition to Death from any state
fsm.AddGlobalTransition(PlayerState.Death)
    .SetCondition(sm => health <= 0)
    .HighestPriority();
```

### State History

Track and navigate previous states:

```csharp
// Go back
if (fsm.CanGoBack())
    fsm.GoBack();

// Go back multiple steps
if (fsm.CanGoBack(3))
    fsm.GoBack(3);

// Go back to specific state
fsm.GoBackToState(PlayerState.Idle);

// Peek without transitioning
var prevState = fsm.PeekBackState();

// Find state in history
int stepsBack = fsm.FindInHistory(PlayerState.Idle);

// Configure history
fsm.SetHistoryActive(true);
fsm.StateHistory.SetCapacity(50);
fsm.StateHistory.ClearHistory();
```

### Event System

```csharp
// Define transition on event
fsm.AddTransition(PlayerState.Idle, PlayerState.Rolling)
    .OnEvent("dodge");

// Using enum for type-safety
public enum PlayerEvent { Dodge, Attack, Jump }

fsm.AddTransition(PlayerState.Idle, PlayerState.Rolling)
    .OnEvent(PlayerEvent.Dodge);

// Trigger events
fsm.TriggerEvent("dodge");
fsm.TriggerEvent(PlayerEvent.Dodge);

// Subscribe to FSM events
fsm.StateChanged += (from, to) => GD.Print($"Changed: {from} → {to}");
fsm.TransitionTriggered += (from, to) => GD.Print($"Triggered: {from} → {to}");

// Custom event listeners
fsm.OnEvent("player_hit", () => FlashSprite());
```

### State Templates

Reuse common configurations:

```csharp
// Create template
var combatTemplate = new StateTemplate<PlayerState>()
    .WithEnter(() => StartCombat())
    .WithExit(() => EndCombat())
    .WithTags("combat")
    .WithLock(FSMLockMode.Transition)
    .WithMinDuration(0.2f);

// Apply to multiple states
fsm.AddState(PlayerState.Attacking).ApplyTemplate(combatTemplate);
fsm.AddState(PlayerState.Blocking).ApplyTemplate(combatTemplate);
```

### Bulk Transitions

```csharp
// Add transitions from multiple states
var groundedStates = new[] { 
    PlayerState.Idle, 
    PlayerState.Walking, 
    PlayerState.Running 
};
fsm.AddTransitions(groundedStates, PlayerState.Jumping, sm => JumpPressed());

// Add transitions from all states with tag
fsm.AddTagTransition("grounded", PlayerState.Jumping, sm => JumpPressed());

// Reset transition (return to initial state)
fsm.AddResetTransition(PlayerState.GameOver);

// Self transition (restart same state)
fsm.AddSelfTransition(PlayerState.Attacking)
   .SetCondition(sm => CanCombo());
```

### Cooldowns

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
```

### State Queries

```csharp
// Current state
if (fsm.IsCurrentState(PlayerState.Attacking))
    GD.Print("Currently attacking!");

var currentId = fsm.GetCurrentId();
var currentState = fsm.CurrentState;

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

### Custom Logger

```csharp
public class GodotLogger : ILogger
{
    public void LogError(string text) => GD.PrintErr($"[FSM ERROR] {text}");
    public void LogWarning(string text) => GD.Print($"[FSM WARNING] {text}");
}

var fsm = new StateMachine<PlayerState>(new GodotLogger());
```

---

## 📚 Best Practices

1. **Use Enums**: Always use enums for state IDs for type safety
2. **Hierarchical Organization**: Group related states under parent states
3. **Type-Safe Data**: Create data classes instead of using primitives
4. **Tag Your States**: Use tags to group related states (`"grounded"`, `"combat"`, etc.)
5. **Guards vs Conditions**: Use guards for prerequisites, conditions for triggers
6. **Lock Important States**: Prevent interruption of critical animations/cutscenes
7. **Set Cooldowns**: Prevent ability spam and control pacing
8. **Use Templates**: Share configurations across similar states
9. **Track History**: Enable history for undo systems or debugging
10. **Validate Hierarchy**: Call `ValidateHierarchy()` after setup

---

## 🐛 Debugging Tips

```csharp
// Log all state changes
fsm.StateChanged += (from, to) => GD.Print($"{from} → {to}");

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

---

## 💡 Examples

### Enemy AI with Hierarchical States

```csharp
public enum EnemyState
{
    // Root states
    Patrol,
    Combat,
    
    // Patrol children
    PatrolWalking,
    PatrolIdle,
    
    // Combat children
    CombatChasing,
    CombatAttacking,
    CombatRetreating
}

public partial class Enemy : CharacterBody2D
{
    private StateMachine<EnemyState> fsm;
    private Player player;
    
    public override void _Ready()
    {
        fsm = new StateMachine<EnemyState>();
        
        // Patrol parent
        var patrol = fsm.AddState(EnemyState.Patrol)
            .OnEnter(() => ClearTarget());
        
        // Patrol children
        fsm.AddChildState(EnemyState.Patrol, EnemyState.PatrolWalking)
            .OnUpdate(delta => PatrolMovement(delta));
        
        fsm.AddChildState(EnemyState.Patrol, EnemyState.PatrolIdle)
            .TimeoutAfter(2.0f, EnemyState.PatrolWalking);
        
        patrol.SetDefaultChild(EnemyState.PatrolWalking);
        
        // Combat parent
        var combat = fsm.AddState(EnemyState.Combat)
            .OnEnter(() => SetTarget(player));
        
        // Combat children
        fsm.AddChildState(EnemyState.Combat, EnemyState.CombatChasing)
            .OnUpdate(delta => ChaseTarget(delta));
        
        fsm.AddChildState(EnemyState.Combat, EnemyState.CombatAttacking)
            .OnEnter(() => Attack())
            .MinDuration(0.5f);
        
        fsm.AddChildState(EnemyState.Combat, EnemyState.CombatRetreating)
            .OnUpdate(delta => Retreat(delta));
        
        combat.SetDefaultChild(EnemyState.CombatChasing);
        
        // Transitions
        fsm.AddTransition(EnemyState.Patrol, EnemyState.Combat)
            .SetCondition(sm => PlayerInRange());
        
        fsm.AddTransition(EnemyState.Combat, EnemyState.Patrol)
            .SetCondition(sm => PlayerTooFar());
        
        fsm.AddTransition(EnemyState.CombatChasing, EnemyState.CombatAttacking)
            .SetCondition(sm => InAttackRange());
        
        fsm.AddTransition(EnemyState.CombatAttacking, EnemyState.CombatChasing)
            .SetCondition(sm => !InAttackRange());
        
        fsm.SetInitialId(EnemyState.Patrol);
        fsm.Start();
    }
    
    public override void _Process(double delta)
    {
        fsm.UpdateIdle((float)delta);
    }
}
```

### Combat System with Data

```csharp
public class CombatData
{
    public int ComboCount;
    public float LastAttackTime;
    public int Damage;
}

public partial class Fighter : CharacterBody2D
{
    private StateMachine<FighterState> fsm;
    
    public override void _Ready()
    {
        fsm = new StateMachine<FighterState>();
        
        // Setup combat state with data
        fsm.AddState(FighterState.Attacking)
            .OnEnter(() => StartAttack())
            .OnUpdate(delta => UpdateAttack(delta))
            .OnExit(() => EndAttack())
            .SetData(new CombatData { Damage = 10 })
            .MinDuration(0.3f)
            .SetCooldown(1.0f);
        
        // Self-transition for combos
        fsm.AddSelfTransition(FighterState.Attacking)
            .SetCondition(sm => CanCombo())
            .OnTrigger(() => IncrementCombo());
        
        fsm.Start();
    }
    
    private void StartAttack()
    {
        if (fsm.CurrentState.TryGetData<CombatData>(out var data))
        {
            data.LastAttackTime = Time.GetTicksMsec() / 1000f;
            GD.Print($"Starting combo #{data.ComboCount + 1}");
        }
    }
    
    private void IncrementCombo()
    {
        if (fsm.CurrentState.TryGetData<CombatData>(out var data))
        {
            data.ComboCount++;
            data.Damage = 10 + (data.ComboCount * 5);
            GD.Print($"Combo! Damage: {data.Damage}");
        }
    }
}
```

---

## 📜 License

**Custom License** - See [LICENSE](LICENSE) file for full terms.

### TL;DR - You can:
✅ Use this FSM freely in any project (personal or commercial)  
✅ Modify it for your needs  
✅ Ship it in your games without crediting in-game

### You must:
⚠️ Give credit if you **advertise** that your game uses this FSM  
⚠️ Not claim it as your own creation

### Examples:

**❌ No credit needed:**
- Just using the FSM in your game
- Releases on Steam without mentioning the FSM

**✅ Credit needed:**
- Game description: "Built with a powerful FSM system..."
- YouTube tutorial about this FSM
- **Credit format**: `Uses Stately by [Ahmed GD]` or link to GitHub

**❌ Prohibited:**
- Claiming as your own
- Renaming and redistributing as original work

---

**Simple rule:** Use freely, credit if you promote it. Don't steal. ✌️

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!

Feel
