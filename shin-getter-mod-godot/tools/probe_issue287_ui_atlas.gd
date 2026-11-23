extends SceneTree

const NATIVE_ATLAS := "res://images/atlases/ui_atlas.png"
const NATIVE_SHEET := "res://images/atlases/ui_atlas.tpsheet"
const MOD_ATLAS := "res://images/atlases/shin_getter_ui_atlas.png"
const MOD_ENERGY := "res://images/atlases/ui_atlas.sprites/card/energy_shin_getter.tres"
const NATIVE_SPRITES := [
	"card/card_frame_attack_s.png", "popup_vertical.png",
	"popup_confirm_button.png", "popup_cancel_button.png", "top_bar/top_bar.png",
]

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2 or not ProjectSettings.load_resource_pack(args[0], true):
		push_error("Usage (empty Godot project): probe_issue287_ui_atlas.gd -- game.pck mod.pck")
		quit(1)
		return
	var sheet = JSON.parse_string(FileAccess.get_file_as_string(NATIVE_SHEET))
	var native_sha := FileAccess.get_sha256(NATIVE_ATLAS + ".import")
	var sheet_sha := FileAccess.get_sha256(NATIVE_SHEET)
	var before := ResourceLoader.load(NATIVE_ATLAS, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP) as Texture2D
	if not sheet is Dictionary or native_sha.is_empty() or before == null:
		push_error("Native UI atlas or region metadata unavailable")
		quit(1)
		return
	if not ProjectSettings.load_resource_pack(args[1], true):
		push_error("Could not mount candidate mod PCK")
		quit(1)
		return
	var after := ResourceLoader.load(NATIVE_ATLAS, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP) as Texture2D
	if after == null:
		push_error("Native atlas no longer loads")
		quit(1)
		return
	var fits: Dictionary = {}
	for page in sheet["textures"]:
		if page["image"] != NATIVE_ATLAS.get_file():
			continue
		for sprite in page["sprites"]:
			if sprite["filename"] in NATIVE_SPRITES:
				var r: Dictionary = sprite["region"]
				fits[sprite["filename"]] = r["x"] >= 0 and r["y"] >= 0 \
					and r["x"] + r["w"] <= after.get_width() and r["y"] + r["h"] <= after.get_height()
	var native_ok := native_sha == FileAccess.get_sha256(NATIVE_ATLAS + ".import") \
		and sheet_sha == FileAccess.get_sha256(NATIVE_SHEET) and before.get_size() == after.get_size() \
		and fits.size() == NATIVE_SPRITES.size() and not fits.values().has(false)
	print("NATIVE_UI_ATLAS before=", before.get_size(), " after=", after.get_size(), " regions=", fits)
	if not native_ok:
		push_error("Mod PCK changed the active native atlas or made native regions out of bounds")
		quit(1)
		return

	var mod := ResourceLoader.load(MOD_ATLAS, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP) as Texture2D
	var energy := ResourceLoader.load(MOD_ENERGY, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP) as AtlasTexture
	var mod_ok := mod != null and mod.get_size() == Vector2(899, 276) and energy != null \
		and energy.atlas != null and energy.atlas.resource_path == MOD_ATLAS \
		and energy.region == Rect2(828, 0, 71, 79)
	if not mod_ok:
		push_error("Getter UI atlas or energy icon was not preserved")
		quit(1)
		return
	print("GETTER_UI_ATLAS size=", mod.get_size(), " energy=", energy.region, " PASS")
	quit(0)
