extends SceneTree

const REQUIRED_RESOURCES := [
	"res://images/atlases/ui_atlas.sprites/card/energy_shin_getter.tres",
	"res://scenes/vfx/card_trail_shin_getter.tscn",
	"res://scenes/creature_visuals/shin_getter.tscn",
	"res://scenes/combat/energy_counters/shin_getter_energy_counter.tscn",
	"res://scenes/ui/character_icons/shin_getter_icon.tscn",
	"res://scenes/merchant/characters/shin_getter_merchant.tscn",
	"res://scenes/rest_site/characters/shin_getter_rest_site.tscn",
]


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
		else:
			print("MOD_RESOURCE_OK: %s" % path)

	quit(1 if failed else 0)
