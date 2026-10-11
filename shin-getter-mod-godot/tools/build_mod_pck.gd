extends SceneTree

const EXCLUDED_DIRS := [".git", ".godot", "Godot", "src", "lib", "obj", "bin", "tools", "art", "art_sources", "build"]
const EXCLUDED_EXTENSIONS := ["cs", "csproj", "sln", "pdb", "dll", "cache", "md", "md5", "pck", "uid", "psd", "mp4", "gif", "py", "pyc", "ps1"]
const IMPORTED_SOURCE_EXTENSIONS := ["png", "jpg", "jpeg", "webp", "svg", "wav", "mp3", "ogg"]
const SOURCE_FREE_IMPORTERS := ["texture", "wav", "mp3", "oggvorbis"]

var packer := PCKPacker.new()
var packed: Dictionary = {}
var imports: Array[Dictionary] = []
var skipped: Array[Dictionary] = []
var failed := false
var binary_atlases := 0

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 1 or args.size() > 2:
		push_error("Usage: build_mod_pck.gd -- output.pck [report.json]")
		quit(1)
		return
	var output := ProjectSettings.globalize_path(args[0])
	for artifact in args:
		var local := ProjectSettings.localize_path(ProjectSettings.globalize_path(artifact))
		if local.begins_with("res://") and local.trim_prefix("res://").get_slice("/", 0) not in EXCLUDED_DIRS:
			ensure(false, "Output must be outside scanned resources (or in build/): " + artifact)
			quit(1)
			return
	if not ensure(packer.pck_start(output) == OK, "Cannot create PCK: " + output):
		quit(1)
		return
	visit("res://")
	if failed or not ensure(packer.flush() == OK, "PCK flush failed"):
		quit(1)
		return
	var payload_bytes := 0
	var source_bytes_removed := 0
	for item in packed.values():
		payload_bytes += int(item["bytes"])
	for item in skipped:
		if item["reason"] == "imported-source-copy":
			source_bytes_removed += int(item["bytes"])
	var report := {"status": "PACK_COMPLETE", "files": packed.values(), "imports": imports, "skipped": skipped,
		"binary_atlases": binary_atlases, "payload_bytes": payload_bytes,
		"source_bytes_removed": source_bytes_removed, "pck_bytes": FileAccess.open(output, FileAccess.READ).get_length(),
		"pck_sha256": FileAccess.get_sha256(output)}
	if args.size() == 2:
		var stream := FileAccess.open(args[1], FileAccess.WRITE)
		if not ensure(stream != null, "Cannot write packaging report"):
			quit(1)
			return
		stream.store_string(JSON.stringify(report, "\t"))
		stream.close()
	print("MOD_PCK_COMPLETE files=", packed.size(), " binary_atlases=", binary_atlases,
		" pck_bytes=", report["pck_bytes"], " source_bytes_removed=", source_bytes_removed)
	quit(0)

func ensure(condition: bool, message: String) -> bool:
	if not condition:
		failed = true
		push_error(message)
	return condition

func visit(path: String) -> void:
	if failed or FileAccess.file_exists(path + ".gdignore"):
		return
	var directory := DirAccess.open(path)
	if not ensure(directory != null, "Cannot enumerate " + path):
		return
	var children := directory.get_directories()
	children.sort()
	for child in children:
		if child not in EXCLUDED_DIRS:
			visit(path.path_join(child) + "/")
	var filenames := directory.get_files()
	filenames.sort()
	for filename in filenames:
		if failed:
			return
		var resource := path.path_join(filename)
		if filename in ["project.godot", "export_presets.cfg"] or filename.ends_with(".import"):
			continue
		if filename.get_extension() in EXCLUDED_EXTENSIONS or filename.begins_with("sprite_0"):
			continue
		if resource.begins_with("res://images/atlases/") and filename.ends_with(".tres"):
			add_binary_atlas(resource)
		elif FileAccess.file_exists(resource + ".import"):
			add_import(resource)
		elif filename.get_extension() in IMPORTED_SOURCE_EXTENSIONS:
			ensure(false, "Resource has no import metadata; run Godot import first: " + resource)
		else:
			add_file(resource, resource)

func add_import(source: String) -> void:
	var config := ConfigFile.new()
	if not ensure(config.load(source + ".import") == OK, "Invalid import metadata: " + source):
		return
	if not ensure(str(config.get_value("deps", "source_file", "")) == source, "Import source mismatch: " + source):
		return
	var destinations = config.get_value("deps", "dest_files", [])
	if not ensure(destinations.size() > 0, "Import destinations missing: " + source):
		return
	for destination in destinations:
		if not ensure(str(destination).begins_with("res://.godot/imported/") and str(destination).simplify_path() == str(destination), "Unsafe import destination: " + str(destination)):
			return
		add_file(str(destination), str(destination))
		if failed:
			return
	add_file(source + ".import", source + ".import")
	var importer := str(config.get_value("remap", "importer", ""))
	imports.append({"source": source, "config": source + ".import", "destinations": destinations, "importer": importer})
	# Direct-read JSON stays intact; only known remapped media omit their source copies.
	if importer in SOURCE_FREE_IMPORTERS and source != "res://icon.svg":
		skipped.append({"path": source, "reason": "imported-source-copy", "bytes": FileAccess.open(source, FileAccess.READ).get_length()})
	else:
		add_file(source, source)

func add_binary_atlas(resource: String) -> void:
	var texture := load(resource) as AtlasTexture
	if not ensure(texture != null and texture.atlas != null, "Invalid atlas: " + resource):
		return
	var binary := "res://.godot/exported/mod_pck/" + resource.trim_prefix("res://").trim_suffix(".tres") + ".res"
	if not ensure(DirAccess.make_dir_recursive_absolute(binary.get_base_dir()) == OK, "Cannot create atlas directory"):
		return
	if not ensure(ResourceSaver.save(texture, binary) == OK, "Cannot serialize atlas: " + resource):
		return
	var remap := binary + ".remap"
	var stream := FileAccess.open(remap, FileAccess.WRITE)
	if not ensure(stream != null, "Cannot create atlas remap"):
		return
	stream.store_string("[remap]\n\npath=\"" + binary + "\"\n")
	stream.close()
	add_file(resource + ".remap", remap)
	add_file(binary, binary)
	binary_atlases += 1

func add_file(resource: String, source: String) -> void:
	if failed or packed.has(resource):
		return
	if not ensure(FileAccess.file_exists(source), "Missing resource dependency: " + source):
		return
	if not ensure(packer.add_file(resource, source) == OK, "Cannot pack " + resource):
		return
	packed[resource] = {"path": resource, "bytes": FileAccess.open(source, FileAccess.READ).get_length(), "sha256": FileAccess.get_sha256(source)}
