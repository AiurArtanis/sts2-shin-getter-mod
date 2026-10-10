#!/usr/bin/env python3
"""Static first-release epoch boundary; no player files or game processes touched."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
session = (ROOT / 'src/Services/ShinGetterBondSession.cs').read_text(encoding='utf-8-sig')
dto = (ROOT / 'src/Services/ShinGetterBondSave.cs').read_text(encoding='utf-8-sig')
assert 'public string ProgressEpoch { get; set; } = "";' in dto, 'RED: missing persisted first-release epoch'
assert 'ReleaseProgressEpoch = "v1.3.0"' in session
assert 'CreateWithLegacyAcquaintances' not in session, 'Do not import previous acquaintances'
read = session[session.index('private ShinGetterBondSave Read()'):session.index('private bool Commit(')]
assert 'save.ProgressEpoch.Length == 0' in read and 'CreateReleaseSave(save.Revision)' in read
assert read.index('save.ProgressEpoch.Length == 0') < read.index('ValidatePending(save)')
assert 'save.ProgressEpoch != ReleaseProgressEpoch' in read
assert 'ReadReliableHistories' not in read and 'File.Write' not in read and 'File.Copy' not in read
assert 'ShinGetterMod.json' not in session, 'Progress epoch must not reset on every manifest update'
commit = session[session.index('private bool Commit('):]
assert 'next.ProgressEpoch = ReleaseProgressEpoch' in commit
assert '_needsReleaseReset' in commit and '.pre-v1.3.0.' in commit and 'File.Copy(_path,' in commit
assert commit.index('using var transactionLock') < commit.index('File.Copy(_path,') < commit.index('File.Replace') < commit.index('_save = next')
assert '_needsReleaseReset = false' in commit
acquaintance = session[session.index('private static bool IsAcquainted'):session.index('private static ShinGetterBondSave CreateReleaseSave')]
assert 'LegacyAcquaintances' not in acquaintance
print('issue#206 one-time v1.3.0 zero-progress gate PASS (not player acceptance)')
