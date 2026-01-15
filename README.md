# 🎮 Stately

> Elegant State Machine for Godot C#

[![License: Custom](https://img.shields.io/badge/license-Custom-blue.svg)](LICENSE)
[![Godot 4+](https://img.shields.io/badge/Godot-4%2B-blue)](https://godotengine.org/)

A powerful, type-safe finite state machine library with full support for 
hierarchical states, smart transitions, and comprehensive manual control.

## ✨ Features

- **Type-Safe**: Generic implementation using C# enums for state IDs
- **Hierarchical States**: Full support for nested states with unlimited depth
- **Smart Transitions**: Automatic child resolution when transitioning to parent states
- **Type-Based Data Storage**: No boxing for reference types, cleaner API
- **Comprehensive Manual Control**: Force transitions, validate before transitioning, and more
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
3. The namespace is `Stately`

## 🚀 Quick Start

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
        fsm = new StateMachine<PlayerState>();

        fsm.AddState(PlayerState.Idle)
            .OnEnter(() => GD.Print("Entered Idle"))
            .OnUpdate(delta => GD.Print("Idling..."))
            .OnExit(() => GD.Print("Exited Idle"));

        fsm.AddTransition(PlayerState.Idle, PlayerState.Walking)
            .SetCondition(sm => Input.IsActionPressed("move_right"));

        fsm.SetInitialId(PlayerState.Idle);
        fsm.Start();
    }

    public override void _Process(double delta)
    {
        fsm.UpdateIdle((float)delta);
    }
}
```

For complete documentation and advanced features, see the [full documentation](https://github.com/[YourUsername]/Stately).

## 📜 License

Custom License - See [LICENSE](LICENSE) file for full terms.

### TL;DR:
✅ Use Stately freely in any project (personal or commercial)  
✅ Modify it for your needs  
✅ Ship it in your games without crediting in-game  

⚠️ Give credit if you **advertise** that your game uses Stately  
⚠️ Don't claim it as your own creation  

**Simple rule:** Use freely, credit if you promote it. Don't steal. ✌️

## 🤝 Contributing

Contributions welcome! Submit PRs, report bugs, or suggest features.

## ⭐ Support

If Stately helps your project, star this repository and spread the word!

---

**Created by [Your Name]** • Built for Godot 4 C#
