class_name StateMachine

enum ProcessMode { 
	IDLE, FIXED
}

enum LockMode {
	NONE,
	TRANSITION,
	FULL
}

signal state_changed(from_id: int, to_id: int)
signal transition_triggered(from: int, to: int)
signal timeout_blocked(id: int)
signal state_timeout(id: int)

const MAX_TRANSITION_QUEUE_SIZE: int = 20
const TRANSITION_DATA_ID: String = "45u349gng668934u89grg85"

var _history: StateHistory = StateHistory.new()

var _states_enum: Dictionary
var _states: Dictionary[int, State] = {}
var _global_data: Dictionary[String, Variant] = {}

var _global_transitions: Array[Transition] = []
var _cached_sorted_transitions: Array[Transition] = []
var _pending_transitions: Array[int] = []

var _event_listeners: Dictionary[String, Array] = {}
var _pending_events: Array[String] = []

var _current_state: State

var _initial_id: int
var _previous_id: int = -1

var _initialized: bool
var _has_previous_state: bool
var _paused: bool
var _is_transitioning: bool
var _is_processing_event: bool
var _started: bool

var _state_time: float
var _last_state_time: float

var _timers_process_mode: ProcessMode = ProcessMode.IDLE

func _init(_enum: Dictionary) -> void:
	_states_enum = _enum

func set_cooldown_timers_process_mode(mode: ProcessMode) -> void:
	_timers_process_mode = mode

func add_state(id: int) -> State:
	if _states.has(id):
		push_error("state with id: (%s) already exists" % id)
		return _states[id]
	
	var state: State = State.new(id)
	_states[id] = state
	
	if !_initialized:
		set_initial_id(id)
	
	if _initialized && _states.has(_initial_id):
		state.set_timeout_id(_initial_id)
	return state

func start() -> void:
	if _initialized:
		_perform_transition(_initial_id, true)
	_started = true

func remove_state(id: int) -> bool:
	if !_states.has(id):
		push_warning("State with id: (%s) does not exist" % id)
		return false
	
	if _states.is_empty():
		push_error("Cannot remove the last state in the state machine")
		return false
	
	if _initial_id == id:
		var new_id: int = _states.values().front()
		set_initial_id(new_id)
	
	if _current_state != null && _current_state.id == id:
		reset()
	
	for state: State in _states.values():
		state.remove_transition(id)
	_global_transitions = _global_transitions.filter(func(t: Transition) -> bool: return t.to != id)
	
	_states.erase(id)
	sort_transitions()
	
	return true

func reset() -> bool:
	if _states.is_empty():
		push_warning("Can not reset and empty state machine")
		return false
	
	if !_initialized:
		push_warning("State Machine is not _initialized - call set_initial_id() first")
		return false
	
	_history.clear()
	_perform_transition(_initial_id)
	_has_previous_state = false
	_previous_id = -1
	
	return true

func set_initial_id(id: int) -> void:
	if !_states.has(id):
		push_error("state with id: (%s) does not exist" % id)
		return
	_initialized = true
	_initial_id = id
	_current_state = _states[id]

func restart_current_state(call_enter: bool = true, call_exit: bool = false) -> void:
	if _current_state.is_locked():
		push_warning("Can't restart current state with id: %s since it's locked !" % _states_enum.keys()[_current_state.id])
		return
	
	if call_enter && _current_state.enter.is_valid(): _current_state.enter.call()
	if call_exit && _current_state.exit.is_valid(): _current_state.exit.call()
	
	reset_state_time()

func reset_state_time() -> void:
	_last_state_time = _state_time
	_state_time = 0.0

func try_transition_to(id: int, condition: Callable = Callable(), data: Variant = null) -> bool:
	if !min_time_exceeded() || (condition.is_valid() && !condition.call()):
		return false
	
	if !_states.has(id):
		return false
	
	if data != null:
		set_data(TRANSITION_DATA_ID, data)
	
	_perform_transition(id)
	return true

func go_back(steps: int = 1) -> bool:
	if steps < 1:
		push_warning("GoBack steps must be at least 1")
		return false
	
	if _current_state == null || _current_state.is_locked():
		push_warning("Cannot go back: current state is locked or null")
		return false
	
	if _history.entry_size < steps:
		push_warning("Cannot go back %s steps: only {history.CurrentSize} entries in history" % steps)
		return false
	
	var target_index: int = _history.entry_size - steps
	if target_index < 0:
		return false
	
	var target_entry: StateHistory.HistoryEntry = _history.get_entry(target_index)
	
	if !_states.has(target_entry.state_id):
		push_warning("Cannot go back: target state %s no longer exists" % _states_enum.keys()[target_entry.state_id])
		return false
	
	_history.remove_range(target_index, _history.entry_size - target_index)
	
	_perform_transition(target_entry.state_id, false, true)
	return true

func can_go_back() -> bool:
	return _history.entry_size > 0 && _current_state != null && !_current_state.is_locked()

func peek_back_state(steps: int) -> int:
	if steps < 1 || _history.entry_size < steps:
		push_error("Steps have exceeded the current entry size")
		return -1
	
	var target_index: int = _history.entry_size - steps
	return _history.get_entry(target_index).state_id

func find_in_history(state_id: int) -> int:
	for i: int in range(_history.entry_size, -1, -1):
		if _history.get_entry(i).state_id == state_id:
			return _history.entry_size - i
	return -1

func go_back_state(state_id: int) -> bool:
	var steps: int = find_in_history(state_id)
	if steps < 0:
		push_warning("State %s not found in history" % get_state_name(state_id))
		return false
	return go_back(steps)

func _perform_transition(id, bypass_exit: bool = false, bypass_history: bool = false) -> void:
	if _is_transitioning:
		if _pending_transitions.size() >= MAX_TRANSITION_QUEUE_SIZE:
			push_error("Too many queued transitions (%d)! Possible infinite loop?" % MAX_TRANSITION_QUEUE_SIZE)
			return
		_pending_transitions.append(id)
		return
	
	if !_states.has(id):
		push_warning("Can not change state to %s as it does not exist" % id)
		return
	
	_is_transitioning = true
	
	var can_exit = bypass_exit && _current_state != null && !_current_state.is_locked()
	var record_history: bool = _history.is_active && !bypass_history && _current_state != null
	
	if record_history:
		_history.create_new_entry(_current_state.id, _state_time)
	
	if can_exit:
		_safe_call(_current_state.exit)
	
	reset_state_time()
	
	if _current_state != null:
		_previous_id = _current_state.id
		_has_previous_state = true
		
		if !bypass_exit:
			_current_state.start_cooldown()
	
	_current_state = _states[id]
	_safe_call(_current_state.enter)
	
	sort_transitions()
	
	if _initialized:
		state_changed.emit(_previous_id, _current_state.id)
	
	if _pending_transitions.size() > 0:
		var next_id = _pending_transitions.pop_front()
		_is_transitioning = false
		_perform_transition(next_id)
	else:
		_is_transitioning = false
	
	remove_data(TRANSITION_DATA_ID)

func _safe_call(callable: Callable, ...args: Array) -> bool:
	if callable.is_valid():
		if args.is_empty():
			callable.call()
		else:
			callable.callv(args)
		return true
	return false

func add_transition(from: int, to: int) -> Transition:
	if !_is_id_valid(from):
		push_error("From state doesn't exist")
		return null
	
	if !_states.has(to):
		push_error("To state doesn't exist")
		return null
	
	var transition: Transition = _states[from].add_transition(to)
	
	sort_transitions()
	return transition

func add_reset_transition(from: int) -> Transition:
	if !_initialized:
		push_error("Cannot add reset transition: no initial state set. Call add_state() first.")
		return null
	
	if !_is_id_valid(from):
		return null
	
	return add_transition(from, _initial_id)

func add_self_transition(id: int) -> void:
	return add_transition(id, id)

func add_transitions(from: Array[int], to: int, condition: Callable) -> void:
	if from.is_empty():
		push_error("from array is empty or null")
		return
	
	for i: int in range(from.size()):
		add_transition(i, to).set_condition(condition)

func add_global_transition(to: int) -> Transition:
	if !_states.has(to):
		push_error("To State does not exist")
		return null
	
	var transition: Transition = Transition.new(-1, to)
	_global_transitions.append(transition)
	
	sort_transitions()
	return transition

func remove_transition(from: int, to: int) -> bool:
	if !_is_id_valid(from):
		return false
	
	var original_size: int = _states[from].transitions.size()
	_states[from].transitions = _states[from].transitions.filter(func(t: Transition) -> bool: return t.to != to)
	
	var removed: int = original_size - _states[from].transitions.size()
	
	sort_transitions()
	return removed > 0

func remove_global_transition(to: int) -> bool:
	var original_size: int = _global_transitions.size()
	
	_global_transitions = _global_transitions.filter(func(t: Transition) -> bool: return t.to != to)
	var removed: int = original_size - _global_transitions.size()
	
	sort_transitions()
	return removed > 0

func clear_transitions_from(id: int) -> void:
	if !_states.has(id):
		push_error("State with id: %s does not exist" % [_states_enum.keys()[id]])
		return
	_states[id].transitions.clear()
	sort_transitions()

func clear_transitions() -> void:
	for state: State in _states.values():
		state.transitions.clear()
	sort_transitions()

func clear_global_transitions() -> void:
	_global_transitions.clear()
	sort_transitions()

func sort_transitions() -> void:
	_cached_sorted_transitions.clear()
	
	if _current_state != null:
		_cached_sorted_transitions.append_array(_current_state.transitions)
	
	_cached_sorted_transitions.append_array(_global_transitions)
	_cached_sorted_transitions.sort_custom(Transition.compare)

func trigger_event(event_name: String) -> void:
	if event_name.is_empty():
		push_error("Event Name is invalid")
		return
	_pending_events.append(event_name)

func on_event(event_name: String, callback: Callable) -> void:
	if event_name.is_empty():
		push_error("Event Name is invalid")
		return
	
	if !_event_listeners.has(event_name):
		_event_listeners[event_name] = []
	_event_listeners[event_name].append(callback)

func remove_event_listener(event_name: String, callback: Callable) -> bool:
	if !_event_listeners.has(event_name):
		return false
	
	_event_listeners[event_name].erase(callback)
	
	if _event_listeners[event_name].is_empty():
		_event_listeners.erase(event_name)
	return true

func clear_event_listeners() -> void:
	_event_listeners.clear()

func _process_events() -> void:
	while _pending_events.size() > 0:
		var event_name: String = _pending_events.pop_front()
		
		if _event_listeners.has(event_name):
			var listeners: Array = _event_listeners[event_name]
			var size: int = listeners.size()
			
			for i: int in range(size):
				if listeners[i].is_valid():
					listeners[i].call()
			
		if _cached_sorted_transitions.size() > 0 && !_is_processing_event:
			_check_event_transitions(event_name)

func _check_event_transitions(event_name: String) -> void:
	if _is_processing_event:
		return
	
	_is_processing_event = true
	
	for transition: Transition in _cached_sorted_transitions:
		if transition.event_name.is_empty():
			continue
		
		if transition.event_name != event_name:
			continue
		
		if transition.is_on_cooldown():
			continue
		
		var guard_passed: bool = true
		if transition.guard.is_valid():
			guard_passed = transition.guard.call()
		
		var required_time: float = transition.override_min_time if transition.override_min_time > 0.0 else _current_state.min_time
		var time_requirement_met: bool = transition.force_instant_transition || _state_time > required_time
		
		if guard_passed && time_requirement_met:
			if _states.has(transition.to) && _states[transition.to].is_on_cooldown():
				continue
			
			transition.start_cooldown()
			_perform_transition(transition.to)
			
			transition_triggered.emit(transition.from, transition.to)
			if transition.triggered.is_valid(): transition.triggered.call()
			
			_is_processing_event = false
			return
		
	_is_processing_event = false

func update_idle(delta: float) -> void:
	_process(ProcessMode.IDLE, delta)

func update_fixed(delta: float) -> void:
	_process(ProcessMode.FIXED, delta)

func _process(mode: StateMachine.ProcessMode, delta: float) -> void:
	if !_started:
		push_error("State Machine hasn't started yet, make sure to call start() method first")
		return
	
	if _paused || _current_state == null:
		return
	
	if _timers_process_mode == mode:
		_update_cooldown_timers(delta)
	
	if _current_state.process_mode == mode:
		_state_time += delta
		_safe_call(_current_state.update, delta)
		_check_transitions()
		print("ggg")

func _update_cooldown_timers(delta: float) -> void:
	_history.update_elapsed_time(delta)
	
	if _current_state != null:
		_current_state.update_cooldown(delta)
	
	for state: State in _states.values():
		if state != _current_state:
			state.update_cooldown(delta)
	
	for transition: Transition in _cached_sorted_transitions:
		transition.update_cooldown(delta)

func _check_transitions() -> void:
	if _current_state == null:
		return
	
	_process_events()
	
	var timeout_triggered: bool = _current_state.timeout > 0.0 && _state_time >= _current_state.timeout
	
	if timeout_triggered:
		_on_state_timeout_triggered()
		return
	
	if _current_state.transition_blocked():
		return
	
	if !_cached_sorted_transitions.is_empty():
		_check_transition_loop()

func _on_state_timeout_triggered() -> void:
	if _current_state.is_fully_locked():
		timeout_blocked.emit(_current_state.id)
		return
	
	var timeout_id: int = _current_state.timeout_id
	var from_id: int = _current_state.id
	
	if !_states.has(timeout_id):
		push_error("State with id: %s , does not have a timeout id" % from_id)
		return
	
	var target_index: int = _current_state.transitions.find_custom(func(t: Transition) -> bool: return t.to == timeout_id)
	var timeout_transition: Transition = _current_state.transitions[target_index] if target_index != -1 else null
	
	if timeout_transition != null && timeout_transition.is_on_cooldown():
		timeout_blocked.emit(_current_state.id)
		return
	
	if _states.has(timeout_id) && _states[timeout_id].is_on_cooldown():
		timeout_blocked.emit(_current_state.id)
		return
	
	if timeout_transition != null:
		timeout_transition.start_cooldown()
	
	_safe_call(_current_state.callback)
	state_timeout.emit(from_id)
	transition_triggered.emit(from_id, timeout_id)
	_perform_transition(timeout_id)

func _check_transition_loop() -> void:
	for transition: Transition in _cached_sorted_transitions:
		if transition.is_on_cooldown():
			continue
		
		var guard_passed: bool = true
		if transition.guard.is_valid():
			guard_passed = transition.guard.call()
		
		if !guard_passed:
			continue
		
		var required_time: float = transition.override_min_time if transition.override_min_time > 0.0 else _current_state.min_time
		
		if _state_time < required_time && !transition.force_instant_transition:
			continue
		
		if transition.condition.is_valid() && transition.condition.call():
			if _states.has(transition.to) && _states[transition.to].is_on_cooldown():
				continue
			
			transition.start_cooldown()
			_perform_transition(transition.to)
			
			_safe_call(transition.triggered)
			transition_triggered.emit(transition.from, transition.to)
			return

func set_data(key: String, value: Variant) -> void:
	if key.is_empty():
		push_error("invalid data id")
		return
	_global_data[key] = value

func remove_data(key: String) -> bool:
	if key.is_empty():
		push_error("invalid data id")
		return false
	return _global_data.erase(key)

func get_data(key: String) -> Variant:
	if !_global_data.has(key):
		push_error("Data with key: %s, does not exist" % key)
		return null
	return _global_data[key]

func get_per_transition_data() -> Variant:
	return _global_data.get(TRANSITION_DATA_ID, null)

func get_data_safe(id: String, default = null) -> Variant:
	return _global_data.get(id, default)

func is_active() -> bool:
	return !_paused

func toggle_pause(toggle: bool) -> void:
	_paused = toggle

func pause() -> void:
	_paused = true

func resume() -> void:
	_paused = false

func get_last_state_time() -> float:
	return _last_state_time

func get_state_time() -> float:
	return _state_time

func get_min_state_time() -> float:
	if _current_state == null:
		push_error("Current State equals null, make sure state machine is initialized correctly")
		return -1.0
	return _current_state.min_time

func get_remaining_time() -> float:
	return max(0.0, _current_state.timeout - _state_time) if _current_state != null && _current_state.timeout > 0.0 else -1.0

func get_state(id: int) -> State:
	return _states.get(id)

func get_state_with_tag(tag: String) -> State:
	for state: State in _states.values():
		if state.tags.has(tag):
			return state
	return null

func get_states_with_tag(tag: String) -> Array[State]:
	var result: Array[State] = []
	
	for state: State in _states.values():
		if state.tags.has(tag):
			result.append(state)
	return result

func get_state_with_name(state_name: String) -> State:
	for key: String in _states_enum.keys():
		if key == state_name:
			var index: int = _states_enum[key]
			return _states[index]
	return null

func get_timeout_progress() -> float:
	if _current_state == null || _current_state.timeout <= 0.0:
		return -1.0
	return clamp(_state_time / _current_state.timeout, 0.0, 1.0)

func get_current_id() -> int:
	return _current_state.id if _current_state != null else -1

func get_initial_id() -> int:
	return _initial_id if _initialized else -1

func get_previous_id() -> int:
	return _previous_id if _has_previous_state else -1

func get_state_name(id: int) -> String:
	for key in _states_enum.keys():
		if _states_enum[key] == id:
			return key
	return "Unknown"

func min_time_exceeded() -> bool:
	return _current_state != null && _state_time > _current_state.min_time

func has_transition(from: int, to: int) -> bool:
	if !_is_id_valid(from):
		return false
	return _states[from].transitions.any(func(t: Transition) -> bool: return t.to == to)

func has_global_transition(to: int) -> bool:
	return _global_transitions.any(func(t: Transition) -> bool: return t.to == to)

func is_in_state_with_tag(tag: String) -> bool:
	return _current_state != null && _current_state.tags.has(tag)

func is_current_state(id: int) -> bool:
	return _current_state != null && _current_state.id == id

func is_previous_state(id: int) -> bool:
	return _has_previous_state && _previous_id == id

func get_state_transitions_with_tag(tag: String) -> Array[Transition]:
	var index: int = _states.values().find_custom(func(s: State) -> bool: return s.tags.has(tag))
	return _states[index].transitions

func get_available_transitions() -> Array[Transition]:
	if _current_state == null:
		return []
	return _current_state.transitions

func is_transition_on_cooldown(from: int, to: int) -> bool:
	if !_is_id_valid(from):
		return false
	
	var state: State = _states[from]
	var index: int = state.transitions.find_custom(func(t: Transition) -> bool: return t.to == to)
	
	if index == -1:
		push_error("Transition does not exist")
		return false
	
	return state.transitions[index].is_on_cooldown()

func reset_transition_cooldown(from: int, to: int) -> void:
	if !_is_id_valid(from):
		return
	
	var state: State = _states[from]
	var index: int = state.transitions.find_custom(func(t: Transition) -> bool: return t.to == to)
	
	if index == -1:
		push_error("Transition does not exist")
		return
	
	return state.transitions[index].cooldown.reset()

func is_state_on_cooldown(id: int) -> bool:
	if !_is_id_valid(id):
		return false
	return _states[id].is_on_cooldown()

func reset_state_cooldown(id: int) -> void:
	if !_is_id_valid(id):
		return
	_states[id].cooldown.reset()

func reset_all_cooldowns() -> void:
	for state: State in _states.values():
		state.cooldown.reset()
		
		for transition: Transition in state.transitions:
			transition.cooldown.reset()
	
	for transition: Transition in _global_transitions:
		transition.cooldown.reset()

func get_active_cooldown_count() -> int:
	var count: int = 0
	
	for state: State in _states.values():
		if state.is_on_cooldown():
			count += 1
		
		for transition: Transition in state.transitions:
			if transition.is_on_cooldown():
				count += 1
	return count

func set_history_active(active: bool) -> void:
	_history.set_active(active)

func have_event_listener(event_name: String) -> bool:
	return _event_listeners.has(event_name)

func clear_pending_transitions() -> void:
	_pending_transitions.clear()

func debug_current_transition() -> String:
	var prev: String = _states_enum.keys()[_previous_id]
	var current: String = _states_enum.keys()[_current_state.id]
	return "%s -> %s" % [prev, current]

func debug_all_transitions() -> String:
	var result: Array[String] = []
	for t: Transition in _current_state.transitions:
		result.append("%s -> %s (Priority: %s)" % [get_state_name(t.from), get_state_name(t.to), t.priority])
	
	for t: Transition in _global_transitions:
		result.append("Global -> %s (Priority: %s)" % [get_state_name(t.to), t.priority])
	
	return result.reduce(func(acc, w): return w if acc == "" else acc + "\n" + w, "")

func debug_all__states() -> String:
	var result: Array[String] = []
	for state: State in _states.values():
		result.append(get_state_name(state.id))
	return result.reduce(func(acc, w): return w if acc == "" else acc + "\n" + w, "")

func _is_id_valid(id: int) -> bool:
	if !_states.has(id):
		push_error("Can't find state with id: %s" % get_state_name(id))
		return false
	return true
