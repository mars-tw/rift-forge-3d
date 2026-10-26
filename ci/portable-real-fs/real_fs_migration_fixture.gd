class_name RealFsMigrationFixture
extends RefCounted

const UNIX_OWNER_READ_EXECUTE := 320 # 0500
const UNIX_WRITE_BITS := 146 # owner 0200, group 0020, other 0002
const SUPPORTED_UNIX := ["Linux", "macOS", "FreeBSD", "NetBSD", "OpenBSD"]

var original_meta_path := ""
var fixture_dir := ""
var meta_path := ""
var platform := ""

var _owned := false
var _locked := false
var _saved_unix_permissions := -1
var _saved_read_only := false
var _saved_attribute_valid := false
var _token := ""
var _owned_names: Dictionary = {}


func prepare(source_meta_path: String, unix_mode_before_capture: int = -1) -> Dictionary:
	if _owned or not fixture_dir.is_empty():
		return _result(false, "fixture_already_prepared")
	platform = OS.get_name()
	if platform != "Windows" and not (platform in SUPPORTED_UNIX):
		return _result(false, "unsupported_platform:" + platform)
	if source_meta_path.is_empty():
		return _result(false, "empty_source_meta_path")
	var parent := source_meta_path.get_base_dir()
	var parent_absolute := ProjectSettings.globalize_path(parent)
	if not DirAccess.dir_exists_absolute(parent_absolute):
		return _result(false, "source_parent_missing")
	_token = Crypto.new().generate_random_bytes(16).hex_encode()
	if _token.length() != 32:
		return _result(false, "crypto_token_failed")
	fixture_dir = parent.path_join("qa_content_r3_real_fs_%s_%s" % [str(OS.get_process_id()), _token])
	var absolute_dir := ProjectSettings.globalize_path(fixture_dir)
	if DirAccess.dir_exists_absolute(absolute_dir):
		fixture_dir = ""
		return _result(false, "fixture_collision")
	var create_error := DirAccess.make_dir_absolute(absolute_dir)
	if create_error != OK:
		fixture_dir = ""
		return _result(false, "fixture_create_error:%d" % create_error)
	_owned = true
	original_meta_path = source_meta_path
	meta_path = fixture_dir.path_join("meta.cfg")
	_owned_names[meta_path.get_file()] = true
	if platform in SUPPORTED_UNIX:
		if unix_mode_before_capture >= 0:
			if unix_mode_before_capture <= 0 or (unix_mode_before_capture & UNIX_OWNER_READ_EXECUTE) != UNIX_OWNER_READ_EXECUTE:
				return _result(false, "invalid_requested_initial_unix_permissions:%d" % unix_mode_before_capture)
			var requested_error := FileAccess.set_unix_permissions(absolute_dir, unix_mode_before_capture)
			if requested_error != OK or FileAccess.get_unix_permissions(absolute_dir) != unix_mode_before_capture:
				return _result(false, "cannot_set_requested_initial_unix_permissions:%d" % requested_error)
		_saved_unix_permissions = FileAccess.get_unix_permissions(absolute_dir)
		if _saved_unix_permissions <= 0 or (_saved_unix_permissions & UNIX_OWNER_READ_EXECUTE) != UNIX_OWNER_READ_EXECUTE:
			return _result(false, "invalid_initial_unix_permissions:%d" % _saved_unix_permissions)
	return _result(true, "prepared")


func register_owned_path(path: String) -> bool:
	if not _owned or path.get_base_dir() != fixture_dir:
		return false
	_owned_names[path.get_file()] = true
	return true


func lock_and_probe() -> Dictionary:
	if not _owned:
		return _result(false, "fixture_not_owned")
	if _locked:
		return _result(false, "fixture_already_locked")
	if meta_path.get_base_dir() != fixture_dir or not FileAccess.file_exists(meta_path):
		return _result(false, "owned_target_missing")
	var before := FileAccess.get_file_as_bytes(meta_path)
	var error := OK
	if platform == "Windows":
		_saved_read_only = FileAccess.get_read_only_attribute(meta_path)
		_saved_attribute_valid = true
		error = FileAccess.set_read_only_attribute(meta_path, true)
	else:
		var locked_permissions := _saved_unix_permissions & ~UNIX_WRITE_BITS
		error = FileAccess.set_unix_permissions(ProjectSettings.globalize_path(fixture_dir), locked_permissions)
	if error != OK:
		return _result(false, "lock_error:%d" % error)
	_locked = true
	var denied := false
	if platform == "Windows":
		var stream := FileAccess.open(meta_path, FileAccess.READ_WRITE)
		denied = stream == null
		if stream != null:
			stream.close()
	else:
		var probe_path := fixture_dir.path_join("write-probe-%s" % _token)
		var probe := FileAccess.open(probe_path, FileAccess.WRITE)
		denied = probe == null
		if probe != null:
			probe.close()
			_owned_names[probe_path.get_file()] = true
	var unchanged := FileAccess.get_file_as_bytes(meta_path) == before
	if not denied or not unchanged:
		var restore_result := restore()
		return _result(false, "write_denial_not_proven", {"denied": denied, "target_unchanged": unchanged, "restore": restore_result})
	return _result(true, "write_denial_proven", {"denied": true, "target_unchanged": true})


func restore() -> Dictionary:
	if not _owned:
		return _result(false, "fixture_not_owned")
	if not _locked:
		return _result(true, "already_unlocked")
	var error := OK
	var verified := false
	if platform == "Windows":
		if not _saved_attribute_valid:
			return _result(false, "missing_saved_windows_attribute")
		error = FileAccess.set_read_only_attribute(meta_path, _saved_read_only)
		if error == OK:
			verified = FileAccess.get_read_only_attribute(meta_path) == _saved_read_only
	else:
		if _saved_unix_permissions <= 0 or (_saved_unix_permissions & UNIX_OWNER_READ_EXECUTE) != UNIX_OWNER_READ_EXECUTE:
			return _result(false, "missing_saved_unix_permissions")
		var absolute_dir := ProjectSettings.globalize_path(fixture_dir)
		error = FileAccess.set_unix_permissions(absolute_dir, _saved_unix_permissions)
		if error == OK:
			verified = FileAccess.get_unix_permissions(absolute_dir) == _saved_unix_permissions
	if error != OK or not verified:
		return _result(false, "restore_not_verified:%d" % error)
	_locked = false
	return _result(true, "restore_verified")


func cleanup() -> Dictionary:
	if not _owned:
		return _result(true, "cleanup_noop_not_owned")
	if _locked:
		var restore_result := restore()
		if not bool(restore_result.get("ok", false)):
			return _result(false, "cleanup_restore_failed", {"restore": restore_result})
	var directory := DirAccess.open(fixture_dir)
	if directory == null:
		return _result(false, "owned_directory_missing")
	var foreign: Array[String] = []
	for child in directory.get_directories():
		foreign.append(child + "/")
	for name in directory.get_files():
		if not _is_owned_file(name):
			foreign.append(name)
	if not foreign.is_empty():
		return _result(false, "foreign_entries_refused", {"foreign": foreign})
	for name in directory.get_files():
		var remove_error := DirAccess.remove_absolute(fixture_dir.path_join(name))
		if remove_error != OK:
			return _result(false, "owned_file_remove_failed:%s:%d" % [name, remove_error])
	var remove_dir_error := DirAccess.remove_absolute(fixture_dir)
	if remove_dir_error != OK:
		return _result(false, "owned_directory_remove_failed:%d" % remove_dir_error)
	_owned = false
	return _result(true, "cleanup_complete")


func is_owned() -> bool:
	return _owned


func is_locked() -> bool:
	return _locked


func saved_unix_permissions() -> int:
	return _saved_unix_permissions


func saved_read_only_attribute() -> bool:
	return _saved_read_only


func _is_owned_file(name: String) -> bool:
	if _owned_names.has(name):
		return true
	for base_name in _owned_names.keys():
		if name == str(base_name) + ".r3-backup" or name == str(base_name) + ".r3-journal":
			return true
		if name.begins_with(str(base_name) + ".r3-temp-"):
			return true
	return false


func _result(ok: bool, code: String, extra: Dictionary = {}) -> Dictionary:
	var result := {"ok": ok, "code": code, "owned": _owned, "locked": _locked, "fixture_dir": fixture_dir, "meta_path": meta_path}
	result.merge(extra, true)
	return result
