extends SceneTree

var failed := false
var resources: Array[String] = []

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		push_error("Usage: validate_mod_pck.gd -- input.pck packaging-report.json")
		quit(1)
		return
	var report = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
	if not ensure(report is Dictionary and report.get("status") == "PACK_COMPLETE"
		and report.get("files") is Array and report.get("imports") is Array and report.get("skipped") is Array,
		"Invalid packaging receipt"):
		quit(1)
		return
	if not ensure(FileAccess.get_sha256(args[0]) == report.get("pck_sha256", ""), "Package/receipt SHA-256 mismatch"):
		quit(1)
		return
	if not ensure(ProjectSettings.load_resource_pack(args[0], true), "Cannot load package"):
		quit(1)
		return
	visit("res://")
	var expected: Dictionary = {}
	for entry in report["files"]:
		ensure(not expected.has(entry["path"]), "Duplicate inventory entry: " + str(entry["path"]))
		expected[entry["path"]] = true
		var stream := FileAccess.open(entry["path"], FileAccess.READ)
		if ensure(stream != null, "Missing packaged file: " + str(entry["path"])):
			ensure(stream.get_length() == int(entry["bytes"]), "Packaged size mismatch: " + str(entry["path"]))
			stream.close()
			ensure(FileAccess.get_sha256(entry["path"]) == entry.get("sha256", ""), "Packaged SHA-256 mismatch: " + str(entry["path"]))
	for resource in resources:
		ensure(expected.has(resource), "Unexpected packaged resource: " + resource)
		ensure(not resource.begins_with("res://src/") and not resource.begins_with("res://tools/") and not resource.begins_with("res://art_sources/"), "Development resource leaked")
		ensure(not resource.get_file().begins_with("sprite_0") and not resource.ends_with(".md5"), "Source frame/editor metadata leaked")
	ensure(resources.size() == expected.size(), "Package inventory count mismatch")
	var textures := 0
	var audio := 0
	var atlases := 0
	var json_files := 0
	for imported in report["imports"]:
		var source := str(imported["source"])
		var resource = ResourceLoader.load(source, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP)
		if imported["importer"] == "texture":
			ensure(resource is Texture2D and resource.get_width() > 0 and resource.get_height() > 0, "Texture failed without raw source: " + source)
			textures += 1
		elif imported["importer"] in ["wav", "mp3", "oggvorbis"]:
			ensure(resource is AudioStream and resource.get_length() > 0, "Audio failed without raw source: " + source)
			audio += 1
	for entry in report["files"]:
		var path := str(entry["path"])
		if path.begins_with("res://images/atlases/") and path.ends_with(".tres.remap"):
			var logical_path := path.trim_suffix(".remap")
			var atlas = ResourceLoader.load(logical_path, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP)
			ensure(atlas is AtlasTexture and atlas.atlas != null and atlas.get_width() > 0, "Binary atlas failed: " + logical_path)
			atlases += 1
		if path.ends_with(".json"):
			ensure(JSON.parse_string(FileAccess.get_file_as_string(path)) != null, "Direct-read JSON failed: " + path)
			json_files += 1
	ensure(atlases == int(report["binary_atlases"]), "Binary atlas count mismatch")
	for entry in report["skipped"]:
		if entry["reason"] == "imported-source-copy":
			ensure(not FileAccess.file_exists(entry["path"]), "Redundant source remains: " + str(entry["path"]))
	print("MOD_PCK_VALIDATED textures=", textures, " audio=", audio, " atlases=", atlases,
		" json=", json_files, " files=", resources.size(), " failed=", failed)
	quit(1 if failed else 0)

func ensure(condition: bool, message: String) -> bool:
	if not condition:
		failed = true
		push_error(message)
	return condition

func visit(path: String) -> void:
	var directory := DirAccess.open(path)
	if directory == null:
		return
	for name in directory.get_files():
		if name not in ["project.godot", "validate_mod_pck.gd", "inspect.gd"]:
			resources.append(path.path_join(name))
	for child in directory.get_directories():
		visit(path.path_join(child) + "/")
