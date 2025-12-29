class_name Transition

const MAX_PRIORITY: int = 999999999

static var global_insertion_counter: int = 0

var from: int
var to: int

var condition: Callable
var guard: Callable
var triggered: Callable

var event_name: String
var override_min_time: float = -1.0
var priority: int
var insersion_index: int

var force_instant_transition: bool

func _init(from_id: int, to_id: int) -> void:
	from = from_id
	to = to_id
	
	global_insertion_counter += 1
	insersion_index = global_insertion_counter

func on_triggered(method: Callable) -> Transition:
	triggered = method
	return self

func on_event(event: String) -> Transition:
	event_name = event
	return self

func set_condition(method: Callable) -> Transition:
	condition = method
	return self

func set_guard(method: Callable) -> Transition:
	guard = method
	return self

func set_min_time(duration: float) -> Transition:
	override_min_time = duration
	return self

func set_priority(value: int) -> Transition:
	if priority > MAX_PRIORITY:
		push_warning("Priority can not be higher than the max value")
		priority = 0
		return self
	priority = value
	return self

func set_on_top() -> Transition:
	priority = MAX_PRIORITY
	return self

func force_instant() -> Transition:
	force_instant_transition = true
	return self

func break_instant() -> Transition:
	force_instant_transition = false
	return self

static func compare(a: Transition, b: Transition) -> int:
	var priority_compare: int = b.priority - a.priority
	return priority_compare if priority_compare != 0 else (a.insersion_index - b.insersion_index)
