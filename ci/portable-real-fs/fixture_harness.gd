extends SceneTree

const Fixture := preload("res://real_fs_migration_fixture.gd")

var failures: Array[String] = []
var checks := 0


func _initialize() -> void:
	call_deferred("_run")


func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)
		printerr("FIXTURE_HARNESS_FAIL " + message)


func _run() -> void:
	var guard := Fixture.new()
	if bool(guard.lock_and_probe().get("ok", false)):
		_abort("", false, [guard], "unprepared lock was accepted")
		return
	if not bool(guard.cleanup().get("ok", false)):
		_abort("", false, [guard], "unprepared cleanup was not a no-op")
		return
	var failed_prepare := Fixture.new()
	var missing_parent := "user://fixture_harness_missing_%s/meta.cfg" % Crypto.new().generate_random_bytes(16).hex_encode()
	if bool(failed_prepare.prepare(missing_parent).get("ok", false)):
		_abort("", false, [failed_prepare], "prepare accepted a missing parent")
		return
	if not bool(failed_prepare.cleanup().get("ok", false)):
		_abort("", false, [failed_prepare], "failed prepare cleanup touched non-owned state")
		return

	var token := Crypto.new().generate_random_bytes(16).hex_encode()
	var parent := "user://fixture_harness_parent_%s_%s" % [str(OS.get_process_id()), token]
	var parent_absolute := ProjectSettings.globalize_path(parent)
	if DirAccess.dir_exists_absolute(parent_absolute):
		_abort(parent, false, [], "harness parent collision")
		return
	if DirAccess.make_dir_absolute(parent_absolute) != OK:
		_abort(parent, false, [], "cannot create harness parent")
		return
	var parent_owned := true
	if OS.get_name() != "Windows":
		var parent_mode_error := FileAccess.set_unix_permissions(parent_absolute, 448) # 0700
		if parent_mode_error != OK or FileAccess.get_unix_permissions(parent_absolute) != 448:
			_abort(parent, parent_owned, [], "cannot establish nondefault 0700 harness parent")
			return
	var original_meta := parent.path_join("original.cfg")

	var first := Fixture.new()
	var first_prepare := first.prepare(original_meta, 448 if OS.get_name() != "Windows" else -1)
	if not bool(first_prepare.get("ok", false)):
		_abort(parent, parent_owned, [first], "first fixture prepare failed")
		return
	if bool(first.prepare(original_meta).get("ok", false)):
		_abort(parent, parent_owned, [first], "duplicate prepare was accepted")
		return
	var second := Fixture.new()
	var second_prepare := second.prepare(original_meta)
	if not bool(second_prepare.get("ok", false)):
		_abort(parent, parent_owned, [first, second], "second independent fixture prepare failed")
		return
	if first.fixture_dir == second.fixture_dir:
		_abort(parent, parent_owned, [first, second], "two fixtures reused one directory")
		return

	var before_mode := first.saved_unix_permissions()
	if OS.get_name() != "Windows" and before_mode != 448:
		_abort(parent, parent_owned, [first, second], "helper did not capture requested nondefault 0700 child mode")
		return
	var config := ConfigFile.new()
	config.set_value("echo", "shards", 187)
	config.set_value("future_owner_setting", "keep_me", "unaltered")
	if config.save(first.meta_path) != OK:
		_abort(parent, parent_owned, [first, second], "cannot write owned target")
		return
	var before_read_only := FileAccess.get_read_only_attribute(first.meta_path) if OS.get_name() == "Windows" else false
	var before_bytes := FileAccess.get_file_as_bytes(first.meta_path)
	var lock_result := first.lock_and_probe()
	if not bool(lock_result.get("ok", false)):
		_abort(parent, parent_owned, [first, second], "real write denial was not proven")
		return
	_check(first.is_locked(), "helper did not record locked state")
	_check(FileAccess.get_file_as_bytes(first.meta_path) == before_bytes, "denial probe changed target bytes")
	var restore_result := first.restore()
	if not bool(restore_result.get("ok", false)):
		_abort(parent, parent_owned, [first, second], "exact restore failed")
		return
	_check(not first.is_locked(), "helper remained locked after restore")
	if OS.get_name() == "Windows":
		_check(first.saved_read_only_attribute() == before_read_only, "Windows read-only attribute was not captured")
		_check(FileAccess.get_read_only_attribute(first.meta_path) == before_read_only, "Windows read-only attribute was not restored exactly")
	else:
		_check(FileAccess.get_unix_permissions(ProjectSettings.globalize_path(first.fixture_dir)) == before_mode, "Unix mode was not restored exactly")
	var recovered := ConfigFile.new()
	_check(recovered.load(first.meta_path) == OK, "restored target cannot be loaded")
	_check(recovered.get_value("future_owner_setting", "keep_me", "") == "unaltered", "unknown saved field was lost")

	var foreign_path := first.fixture_dir.path_join("foreign.txt")
	if FileAccess.file_exists(foreign_path):
		_abort(parent, parent_owned, [first, second], "foreign cleanup sentinel already existed")
		return
	var foreign := FileAccess.open(foreign_path, FileAccess.WRITE)
	if foreign == null:
		_abort(parent, parent_owned, [first, second], "cannot create foreign cleanup sentinel")
		return
	foreign.store_string("must remain")
	foreign.close()
	var refused := first.cleanup()
	if bool(refused.get("ok", false)) or not FileAccess.file_exists(foreign_path):
		_abort(parent, parent_owned, [first, second], "cleanup did not refuse/preserve foreign entry")
		return
	if DirAccess.remove_absolute(foreign_path) != OK:
		_abort(parent, parent_owned, [first, second], "cannot remove harness-owned foreign sentinel")
		return
	if not bool(first.cleanup().get("ok", false)):
		_abort(parent, parent_owned, [first, second], "cleanup did not recover after foreign entry removal")
		return

	var second_config := ConfigFile.new()
	second_config.set_value("harness", "second", true)
	if second_config.save(second.meta_path) != OK:
		_abort(parent, parent_owned, [second], "second fixture target write failed")
		return
	if not bool(second.cleanup().get("ok", false)):
		_abort(parent, parent_owned, [second], "second fixture cleanup failed")
		return
	if DirAccess.remove_absolute(parent) != OK:
		_abort(parent, parent_owned, [], "harness parent cleanup failed")
		return

	_complete()


func _abort(parent: String, parent_owned: bool, fixtures: Array, message: String) -> void:
	_check(false, message)
	for fixture in fixtures:
		if fixture != null and fixture.is_owned():
			var cleanup: Dictionary = fixture.cleanup()
			if not bool(cleanup.get("ok", false)):
				_check(false, "abort cleanup failed: " + str(cleanup.get("code", "unknown")))
	if parent_owned and not parent.is_empty() and DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(parent)):
		var directory := DirAccess.open(parent)
		if directory != null and directory.get_files().is_empty() and directory.get_directories().is_empty():
			if DirAccess.remove_absolute(parent) != OK:
				_check(false, "abort could not remove empty owned parent")
	_complete()


func _complete() -> void:
	print("FIXTURE_HARNESS_%s checks=%d failures=%d" % ["VERIFIED" if failures.is_empty() else "BLOCKED", checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
