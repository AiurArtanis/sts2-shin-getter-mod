extends SceneTree

const REQUIRED_RESOURCES := {
	"res://materials/cards/frames/card_frame_shin_getter_mat.tres": false,
	"res://materials/transitions/shin_getter_transition_mat.tres": false,
	"res://shaders/shin_getter_hsv.gdshader": false,
	"res://animations/character_select/shin_getter/character_select_shin_getter_bg.png": false,
	"res://audio/sfx/characters/shin_getter/shin_getter_select.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/transform.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/change_getter_1.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/change_getter_1_switch_on.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/switch_on.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/change_getter_2.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/change_getter_3.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/change_shin_dragon.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_combine_blind.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_getter_beam.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_getter_tomahawk.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_ora_ora_ora.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_return_the_favor.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_roar.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_stay_to_the_end.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_star_slash.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_shining.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/team_spark.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/ryoma_getter_shine.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/hot_blood.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/musashi_avalanche.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/musashi_getter_electric.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/musashi_getter_power.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/musashi_fire_now.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/hayato_getter_drill.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/hayato_supersonic.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/hayato_drill_hurricane.wav": false,
	"res://audio/sfx/characters/shin_getter/voices/hayato_drill_arm.wav": false,
	"res://audio/music/shin_getter/execution_theme.mp3": false,
	"res://images/atlases/ui_atlas.sprites/card/energy_shin_getter.tres": false,
	"res://images/packed/sprite_fonts/shin_getter_energy_icon.png": false,
	"res://images/atlases/card_atlas.sprites/shin_getter/s_g_c_ki.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_ki.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_evolution.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_evolution_engine.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_enable.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_hurricane_temporary_dexterity.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_final_getter_beam_strength_down.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_kusuha_juice.tres": false,
	"res://images/atlases/power_atlas.sprites/s_g_p_delay_damage.tres": false,
	"res://images/enchantments/s_g_e_adaptation.png": false,
	"res://images/enchantments/s_g_e_devolution.png": false,
	"res://images/events/s_g_e_getter_mandala.png": false,
	"res://images/powers/s_g_p_evolution.png": false,
	"res://images/powers/s_g_p_evolution_engine.png": false,
	"res://images/powers/s_g_p_final_getter_beam_strength_down.png": false,
	"res://images/powers/s_g_p_delay_damage.png": false,
	"res://images/atlases/sgr_atlas_shin_getter.png": false,
	"res://images/atlases/sgr_outline_atlas_shin_getter.png": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_getter_furnace.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_emperors_fragment.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_battle_instinct.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_alloy_plate.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_research_notes.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_musashi_clone.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_good_citizen_card.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_go_nagai_smile.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_ken_ishikawa_manuscript.tres": false,
	"res://images/atlases/relic_atlas.sprites/s_g_r_yummy_cookie.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_getter_furnace.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_emperors_fragment.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_battle_instinct.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_alloy_plate.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_research_notes.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_musashi_clone.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_good_citizen_card.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_go_nagai_smile.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_ken_ishikawa_manuscript.tres": false,
	"res://images/atlases/relic_outline_atlas.sprites/s_g_r_yummy_cookie.tres": false,
	"res://images/relics/s_g_r_yummy_cookie.png": false,
	"res://images/atlases/potion_atlas.sprites/s_g_r_transform_potion.tres": false,
	"res://images/atlases/potion_atlas.sprites/s_g_r_kusuha_juice.tres": false,
	"res://images/atlases/potion_atlas.sprites/s_g_r_getter_cold_brew.tres": false,
	"res://images/atlases/potion_outline_atlas.sprites/s_g_r_transform_potion.tres": false,
	"res://images/atlases/potion_outline_atlas.sprites/s_g_r_kusuha_juice.tres": false,
	"res://images/atlases/potion_outline_atlas.sprites/s_g_r_getter_cold_brew.tres": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_atlas.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_point.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_rock.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_paper.png": false,
	"res://images/ui/hands/multiplayer_hand_shin_getter_scissors.png": false,
	"res://images/characters/shin_getter/forms/getter_one_attack/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_one_block/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_one_cast/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_one_dash/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_one_death/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_one_idle/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_two_attack/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_two_block/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_two_cast/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_two_dash/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_two_death/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_two_idle/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_three_attack/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_three_block/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_three_cast/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_three_dash/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_three_death/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/getter_three_idle/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/shin_getter_dragon_attack/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/shin_getter_dragon_block/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/shin_getter_dragon_cast/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/shin_getter_dragon_dash/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/shin_getter_dragon_death/sprite_sheet.png": false,
	"res://images/characters/shin_getter/forms/shin_getter_dragon_idle/sprite_sheet.png": false,
	"res://images/characters/shin_getter/merchant/s_g_o_merchant_ryoma_citizen.png": false,
	"res://images/characters/shin_getter/merchant/s_g_o_merchant_ryoma_normal.png": false,
	"res://scenes/creature_visuals/shin_getter.tscn": true,
	"res://scenes/creature_visuals/shin_getter_one_idle_frames.tres": false,
	"res://scenes/creature_visuals/shin_getter_two_idle_frames.tres": false,
	"res://scenes/creature_visuals/shin_getter_three_idle_frames.tres": false,
	"res://scenes/creature_visuals/shin_getter_dragon_idle_frames.tres": false,
	"res://scenes/vfx/card_trail_shin_getter.tscn": true,
	"res://scenes/combat/energy_counters/shin_getter_energy_counter.tscn": true,
	"res://scenes/ui/character_icons/shin_getter_icon.tscn": false,
	"res://scenes/merchant/characters/shin_getter_merchant.tscn": true,
	"res://scenes/rest_site/characters/shin_getter_rest_site.tscn": true,
}

const EXISTS_ONLY_RESOURCES := [
	# This scene references the mod DLL, which is loaded only during the later game-load validation.
	"res://scenes/screens/char_select/char_select_bg_shin_getter.tscn",
	"res://ShinGetterMod/localization/eng/cards.json",
	"res://ShinGetterMod/localization/eng/powers.json",
	"res://ShinGetterMod/localization/eng/static_hover_tips.json",
	"res://ShinGetterMod/localization/jpn/cards.json",
	"res://ShinGetterMod/localization/jpn/powers.json",
	"res://ShinGetterMod/localization/jpn/static_hover_tips.json",
	"res://ShinGetterMod/localization/zhs/cards.json",
	"res://ShinGetterMod/localization/zhs/powers.json",
	"res://ShinGetterMod/localization/zhs/static_hover_tips.json",
]

const FORBIDDEN_RESOURCES := [
	"res://images/characters/shin_getter/forms/getter_one_idle/sprite_000001.png",
	"res://images/characters/shin_getter/forms/getter_two_idle/sprite_000001.png",
	"res://images/characters/shin_getter/forms/getter_three_idle/sprite_000001.png",
	"res://images/characters/shin_getter/forms/shin_getter_dragon_idle/sprite_000001.png",
	"res://images/characters/shin_getter/forms/shin_getter_one_static.png",
	"res://images/characters/shin_getter/forms/shin_getter_two_static.png",
	"res://images/characters/shin_getter/forms/shin_getter_three_static.png",
	"res://images/characters/shin_getter/forms/shin_getter_dragon_static.png",
]

const FORBIDDEN_RESOURCE_DEPENDENCIES := {
	"res://scenes/creature_visuals/shin_getter.tscn": [
		"res://src/Nodes/Combat/NShinGetterStaticVisuals.cs",
		"res://src/Nodes/Combat/NShinGetterSpriteSequence.cs",
	],
}

const CHARACTER_FRAME_MANIFEST_PATH := "art_sources/characters/shin_getter/forms/frame_manifest.txt"
const EXPECTED_CHARACTER_SOURCE_FRAME_COUNT := 920


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
	for path in EXISTS_ONLY_RESOURCES:
		if not ResourceLoader.exists(path):
			push_error("Unable to find mod resource: %s" % path)
			failed = true
			continue

		print("MOD_RESOURCE_EXISTS: %s" % path)

	for path in FORBIDDEN_RESOURCES:
		if ResourceLoader.exists(path):
			push_error("Forbidden mod resource was exported: %s" % path)
			failed = true
			continue

		print("MOD_RESOURCE_ABSENT: %s" % path)

	if not _validate_forbidden_character_frames(args[0]):
		failed = true

	for path in FORBIDDEN_RESOURCE_DEPENDENCIES:
		var dependencies := ResourceLoader.get_dependencies(path)
		for forbidden_dependency in FORBIDDEN_RESOURCE_DEPENDENCIES[path]:
			var dependency_failed := false
			for dependency in dependencies:
				if dependency.contains(forbidden_dependency):
					push_error("Mod resource %s directly references forbidden runtime script: %s" % [path, forbidden_dependency])
					failed = true
					dependency_failed = true
					break
			if dependency_failed:
				break

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


func _validate_forbidden_character_frames(pck_path: String) -> bool:
	var valid := true
	var checked := 0
	var frame_manifest := _load_character_frame_manifest(pck_path)
	if frame_manifest.is_empty():
		return false
	for action in frame_manifest:
		for frame_number in frame_manifest[action]:
			var path := "res://images/characters/shin_getter/forms/%s/sprite_%06d.png" % [action, frame_number]
			checked += 1
			if ResourceLoader.exists(path):
				push_error("Forbidden per-frame character resource was exported: %s" % path)
				valid = false
	if checked != EXPECTED_CHARACTER_SOURCE_FRAME_COUNT:
		push_error("Character frame manifest has %d entries; expected %d" % [checked, EXPECTED_CHARACTER_SOURCE_FRAME_COUNT])
		valid = false

	if valid:
		print("MOD_CHARACTER_SOURCE_FRAMES_ABSENT: %d" % checked)
	return valid


func _load_character_frame_manifest(pck_path: String) -> Dictionary:
	var repo_path := pck_path.get_base_dir().get_base_dir().get_base_dir()
	var manifest_path := repo_path.path_join(CHARACTER_FRAME_MANIFEST_PATH).simplify_path()
	var file := FileAccess.open(manifest_path, FileAccess.READ)
	if file == null:
		push_error("Unable to read character frame manifest: %s" % manifest_path)
		return {}

	var sequences := {}
	var actions := {}
	var line_number := 0
	while not file.eof_reached():
		line_number += 1
		var line := file.get_line().strip_edges()
		if line.is_empty() or line.begins_with("#"):
			continue
		var assignment := line.split("=", false, 1)
		var declaration := assignment[0].split(" ", false, 1) if assignment.size() == 2 else PackedStringArray()
		if declaration.size() != 2:
			push_error("Invalid character frame manifest declaration at line %d" % line_number)
			return {}
		var kind := declaration[0]
		var name := declaration[1]
		if kind == "sequence":
			var frame_numbers := PackedInt32Array()
			for token in assignment[1].split(",", false):
				if not token.is_valid_int():
					push_error("Invalid frame number at manifest line %d" % line_number)
					return {}
				frame_numbers.append(token.to_int())
			sequences[name] = frame_numbers
		elif kind == "action":
			actions[name] = assignment[1]
		else:
			push_error("Unknown character frame manifest declaration at line %d" % line_number)
			return {}

	var manifest := {}
	for action in actions:
		var sequence_name: String = actions[action]
		if not sequences.has(sequence_name):
			push_error("Character frame action %s references unknown sequence %s" % [action, sequence_name])
			return {}
		manifest[action] = sequences[sequence_name]
	return manifest
