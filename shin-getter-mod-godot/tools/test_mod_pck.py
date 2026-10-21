#!/usr/bin/env python3
"""Exercise the actual Godot packer/validator in small isolated fixture projects."""
import argparse
import json
import struct
import subprocess
import tempfile
import wave
from pathlib import Path

TOOLS = Path(__file__).resolve().parent


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')


def run(godot, project, *args, success=True):
    result = subprocess.run([str(godot), '--headless', '--path', str(project), *map(str, args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=120)
    log = result.stdout + result.stderr
    if success:
        assert result.returncode == 0 and 'ERROR:' not in log, log
    else:
        assert result.returncode != 0, 'Expected failure:\n' + log
    return log


def main(godot):
    with tempfile.TemporaryDirectory(prefix='shin-getter-pck-tests-') as directory:
        root = Path(directory)
        source, empty = root / 'source', root / 'empty'
        for project in (source, empty):
            write(project / 'project.godot', 'config_version=5\n')
        write(source / 'images/test.svg', '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><rect width="16" height="16" fill="green"/></svg>')
        write(source / 'images/atlases/test.tres', '[gd_resource type="AtlasTexture" load_steps=2 format=3]\n'
              '[ext_resource type="Texture2D" path="res://images/test.svg" id="1"]\n'
              '[resource]\natlas=ExtResource("1")\nregion=Rect2(0,0,8,8)\n')
        audio = source / 'audio/test.wav'
        audio.parent.mkdir()
        with wave.open(str(audio), 'wb') as output:
            output.setparams((1, 2, 8000, 0, 'NONE', 'not compressed'))
            output.writeframes(b''.join(struct.pack('<h', (i % 40 - 20) * 500) for i in range(800)))
        write(source / 'data/direct.json', '{"required":true}')
        run(godot, source, '--editor', '--import', '--quit')
        for path in ('src/unused.cs', 'tools/unused.json', 'art_sources/master.png', 'images/sprite_0001.png',
                     '.godot/imported/orphan.ctex', '.godot/imported/editor.md5', 'ignored/.gdignore', 'ignored/unused.json'):
            write(source / path, 'not a runtime dependency')
        package, receipt = root / 'test.pck', root / 'report.json'

        def pack(success=True):
            return run(godot, source, '--script', TOOLS / 'build_mod_pck.gd', '--', package, receipt, success=success)

        def validate(report=receipt, success=True):
            return run(godot, empty, '--script', TOOLS / 'validate_mod_pck.gd', '--', package, report, success=success)

        pack()
        report = json.loads(receipt.read_text())
        paths = {item['path'] for item in report['files']}
        assert 'res://data/direct.json' in paths and 'res://images/atlases/test.tres.remap' in paths
        assert 'res://images/test.svg' not in paths and 'res://audio/test.wav' not in paths
        assert not any('orphan' in path or 'unused' in path or 'sprite_0001' in path or path.endswith('.md5') for path in paths)
        assert len(report['imports']) == 2 and report['binary_atlases'] == 1
        validate()
        pack()
        assert json.loads(receipt.read_text()) == report, 'Same imported source must produce the same receipt/hash'
        print('PASS: runtime remaps/texture/audio/atlas, direct JSON, development exclusions, repeatability')

        tampered = root / 'tampered.json'
        changed = dict(report, pck_sha256='0' * 64)
        write(tampered, json.dumps(changed))
        assert 'SHA-256 mismatch' in validate(tampered, success=False)
        changed = dict(report, files=[dict(item) for item in report['files']])
        changed['files'][0]['sha256'] = '0' * 64
        write(tampered, json.dumps(changed))
        assert 'Packaged SHA-256 mismatch' in validate(tampered, success=False)
        changed = dict(report, files=report['files'][1:])
        write(tampered, json.dumps(changed))
        assert 'Unexpected packaged resource' in validate(tampered, success=False)
        write(tampered, '{}')
        assert 'Invalid packaging receipt' in validate(tampered, success=False)
        print('PASS: wrong package/file hash, incomplete inventory and invalid receipt fail closed')

        write(source / 'images/missing.svg', '<svg/>')
        assert 'no import metadata' in pack(success=False)
        (source / 'images/missing.svg').unlink()
        metadata = source / 'images/test.svg.import'
        original = metadata.read_text()
        metadata.write_text(original.replace('source_file="res://images/test.svg"', 'source_file="res://images/wrong.svg"'))
        assert 'Import source mismatch' in pack(success=False)
        metadata.write_text(original)
        texture_import = next(item for item in report['imports'] if item['source'] == 'res://images/test.svg')
        destination = source / texture_import['destinations'][0].removeprefix('res://')
        assert destination.exists()
        destination.unlink()
        log = pack(success=False)
        assert 'Missing resource dependency' in log or 'Invalid atlas' in log, log
        print('PASS: missing import metadata, wrong source and missing runtime dependency fail closed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True, type=Path)
    main(parser.parse_args().godot)
