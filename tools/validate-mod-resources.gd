extends SceneTree

const REQUIRED_RESOURCES := {
	"res://materials/cards/frames/card_frame_shin_getter_mat.tres": false,
	"res://shaders/shin_getter_hsv.gdshader": false,
	"res://images/atlases/ui_atlas.sprites/card/energy_shin_getter.tres": false,
	"res://images/atlases/card_atlas.sprites/shin_getter/s_g_c_ki.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_ki.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_enable.tres": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_atlas.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_point.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_rock.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_paper.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_scissors.png": false,
	"res://scenes/vfx/card_trail_shin_getter.tscn": true,
	"res://scenes/creature_visuals/shin_getter.tscn": true,
	"res://scenes/combat/energy_counters/shin_getter_energy_counter.tscn": true,
	"res://scenes/ui/character_icons/shin_getter_icon.tscn": false,
	"res://scenes/merchant/characters/shin_getter_merchant.tscn": true,
	"res://scenes/rest_site/characters/shin_getter_rest_site.tscn": true,
}


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("PCK path argument is required.")
		quit(2)
		return

	if not ProjectSettings.load_resource_pack(args[0], true):
		push_error("Unable to load PCK: %s" % args[0])
		quit(3)
		return

	var failed := false
	for path in REQUIRED_RESOURCES:
		var resource := ResourceLoader.load(path)
		if resource == null:
			push_error("Unable to load mod resource: %s" % path)
			failed = true
			continue

		if resource is PackedScene:
			var instance := (resource as PackedScene).instantiate()
			if instance == null:
				push_error("Unable to instantiate mod scene: %s" % path)
				failed = true
				continue
			if REQUIRED_RESOURCES[path] and instance.get_script() == null:
				push_error("Instantiated scene has no root script: %s" % path)
				failed = true
			instance.free()

		print("MOD_RESOURCE_OK: %s" % path)

	quit(1 if failed else 0)
