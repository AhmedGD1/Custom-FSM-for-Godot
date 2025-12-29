class_name State

var id: int
var timeout_id: int

var transitions: Array[Transition] = []

var min_time: float
var timeout: float = -1.0

var update: Callable
var enter: Callable
var exit: Callable
var callback: Callable

var process_mode: StateMachine.ProcessMode = StateMachine.ProcessMode.PHYSICS
var lock_mode: StateMachine.LockMode = StateMachine.LockMode.None

var tags: Array[String] = []
var data: Dictionary[String, Variant] = {}

func _init(new_id: int) -> void:
	id = new_id

func add_transition(to: int) -> Transition:
	var transition: Transition = Transition.new(id, to)
	transitions.append(transition)
	return transition

func remove_transition(to: int) -> bool:
	for t: Transition in transitions:
		if t.to == to:
			transitions.erase(t)
			return true
	return false

func has_transition(to: int) -> bool:
	return transitions.find_custom(func(t: Transition) -> bool: return t.to == to) != -1

func on_update(method: Callable) -> State:
	update = method
	return self

func on_enter(method: Callable) -> State:
	enter = method
	return self

func on_exit(method: Callable) -> State:
	exit = method
	return self

func on_timeout(method: Callable) -> State:
	callback = method
	return self

func set_min_time(duration: float) -> State:
	min_time = max(0.0, duration)
	return self

func set_timeout(duration: float) -> State:
	timeout = duration
	return self

func set_timeout_id(to_id: int) -> State:
	timeout_id = to_id
	return self

func set_process_mode(mode: StateMachine.ProcessMode) -> State:
	process_mode = mode
	return self

func lock(mode: StateMachine.LockMode = StateMachine.LockMode.Full) -> State:
	lock_mode = mode
	return self

func unlock() -> State:
	lock_mode = StateMachine.LockMode.None
	return self

func add_tags(...what: Array) -> State:
	tags.append_array(what)
	return self

func set_data(key: String, value: Variant) -> State:
	data[key] = value
	return self

func get_data(key: String) -> Variant:
	if !data.has(key):
		push_error("Data with key: %s, does not exist" % key)
		return null  # Should return null, not false
	return data[key]  # BUG FIX: was returning false!

func remove_data(key: String) -> bool:
	return data.erase(key)

func is_locked() -> bool:
	return lock_mode != StateMachine.LockMode.None

func is_fully_locked() -> bool:
	return lock_mode == StateMachine.LockMode.Full

func transition_blocked() -> bool:
	return lock_mode == StateMachine.LockMode.Transition

func has_data(key: String) -> bool:
	return data.has(key)

func has_data_with_value(value: Variant) -> bool:
	return data.values().find_custom(func(d): return d == value) != -1

func get_process_mode() -> StateMachine.ProcessMode:
	return process_mode

func has_transitions() -> bool:
	return !transitions.is_empty()

func get_transition_to(to_id: int) -> Transition:
	for t: Transition in transitions:
		if t.to == to_id:
			return t
	return null
