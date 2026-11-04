# issue#281: SRW30 DEVOLUTION BGM

Adds `emperor_fragment` to the existing BGM catalog, immediately before Random.
All five category dropdowns, preview and random playback use the same entry.
Existing selected IDs, category defaults, volume, master switch, local-player
scope and battle cleanup remain unchanged. There are now 24 concrete tracks.

Titles: 皇帝的碎片 / 皇帝の欠片 / Fragment of the Emperor. The English title
is the project adopted translation; an official English BGM UI string has not
been verified. The publisher's SRW30 FAQ 34 identifies the Japanese title with
Getter Robo DEVOLUTION in the Expansion Pack. The source is BGM 155 / W_CGT_0,
CueIndex 6 / StreamAwbId 5 in 103_012.acb / 103_011.awb.

The keyed source WAV has SHA-256
`23f36f183bd78a894bab73989d221b2d107a6dc83f26e36d8ff1d44647dc9f8b`.
The shipped MP3 is 48 kHz, stereo, 192 kbps, matching the four recent STS2
recordings. No gain normalization or musical edits were applied. Conversion
discards 3,127 decoded padding frames after the ACB playable length of 5,286,857
samples. The original WAV remains unchanged.

ACB loop start is 1,008,000 samples (21 s); end is 5,286,857 samples
(110.142854166667 s), at 48 kHz. Track metadata supplies the loop offset to
the duplicated playback stream in preview, encounter and execution playback.
The actual resolved resource path is used, including Random selection; the
first play still starts at zero. Legacy tracks have no explicit override,
so their imported loop offsets are retained.

Runtime asset: `audio/music/shin_getter/album/emperor_fragment.mp3`,
2,645,037 bytes, SHA-256
`9909d2ff0e6e50fb3497386fe855e8ba10d29b6de6dcf4920b65d47ffa7b10e0`.
The catalog manifest records source identity, format, looping and current
spreadsheet mapping. Spreadsheet row 26 is appended; previous rows and the
historical design sheet are preserved. The native resource gate includes
the new asset. No generated `.import` or PCK is supplied by this branch.

Development verification is limited to media decoding, source syntax, JSON,
catalog/asset contracts, spreadsheet preservation and diff checks. It does not
establish Godot playback, audible seamlessness, settings persistence, physical
input or deployment. Main review should check the new MP3 import and length,
all three playback paths (including Random), first-play intro and subsequent
21 s loop, switch/volume behavior and cleanup, then request human audition.
