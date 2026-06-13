import os

def to_snake(name):
    result = ''
    for i, c in enumerate(name):
        if c.isupper() and i > 0 and name[i-1] != '_':
            result += '_'
        result += c
    return result.lower()

statuses = [
    ('SGP_ShinGetterOne',     384, 0),
    ('SGP_TripleUnity',         0, 128),
    ('SGP_ShinGetterThree',     0, 64),
    ('SGP_Indomitable',       320, 128),
    ('SGP_ShinGetterTwo',     448, 0),
    ('SGP_Shade',             320, 0),
    ('SGP_Acceleration',      256, 64),
    ('SGP_ChosenOne',         128, 128),
    ('SGP_Seal',              128, 0),
    ('SGP_WarriorMedal',      256, 128),
    ('SGP_Grapple',           448, 64),
    ('SGP_FightingSpirit',     64, 64),
    ('SGP_InfiniteEvolution',   0, 192),
    ('SGP_Ki',                256, 0),
    ('SGP_HotBlood',          384, 64),
    ('SGP_GetterRayOverflow', 128, 64),
    ('SGP_ShinForm',          320, 192),
    ('SGP_Desperation',       128, 192),
    ('SGP_Airborne',            0, 0),
    ('SGP_Blueprint',         192, 128),
    ('SGP_Wane',               64, 0),
    ('SGP_AwakenedSoul',      192, 192),
    ('SGP_Insight',            64, 128),
    ('SGP_SuperKi',           448, 128),
    ('SGP_Radiation',         192, 0),
    ('SGP_Overload',          192, 64),
    ('SGP_Evolution',         384, 192),
    ('SGP_EvolutionEngine',    64, 192),
    ('SGP_ChainReaction',     320, 64),
    ('SGP_IronWall',          256, 192),
    ('SGP_Tomahawk',          384, 128),
]

tres_dir = r'E:\Work\StS2 Mods\ShinGetterMod\images\atlases\power_atlas.sprites\shin_getter'
os.makedirs(tres_dir, exist_ok=True)

for name, x, y in statuses:
    snake = to_snake(name)
    fp = os.path.join(tres_dir, snake + '.tres')
    with open(fp, 'w', encoding='utf-8') as f:
        f.write('[gd_resource type="AtlasTexture" load_steps=2 format=3]\n')
        f.write('\n')
        f.write('[ext_resource type="Texture2D" path="res://images/atlases/power_atlas_shin_getter.png" id="1"]\n')
        f.write('\n')
        f.write('[resource]\n')
        f.write('atlas = ExtResource("1")\n')
        f.write(f'region = Rect2({x}, {y}, 64, 64)\n')

print(f'Created {len(statuses)} .tres files')
