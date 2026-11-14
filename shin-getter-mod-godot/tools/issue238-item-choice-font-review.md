# issue#238 item-choice font correction

The event item overlay creates a native `MegaRichTextLabel` for its title and for each item row. Official109 requires an explicit `normal_font` override when each label enters `_Ready`; a theme font size alone does not satisfy that assertion. The first two-item runtime case reported three such errors.

The correction loads the existing native `res://themes/kreon_regular_glyph_space_one.tres` through `PreloadManager.Cache` and applies it to both label construction sites before their respective `AddChild` calls. No font asset or PCK change is required. Native `MegaRichTextLabel._Ready -> RefreshFont` remains intact: official109 retains Kreon for English and applies its own regular/bold/italic language substitutions for Simplified Chinese and Japanese. This source correction does not establish visual glyph or runtime acceptance.

Focused source check:

```powershell
python tools/validate_issue_238.py --self-test-item-choice-fonts
```

The check rejects six mutations: a missing override, a wrong font key and an override after attachment, independently for the title and item row. It does not launch the engine or replace the original four runtime cases. Selection synchronization, completion tasks, focus, cancellation, payment and reward behavior are unchanged.

Developer checks: focused source gate and diff-check pass; one product compilation against the actual official109 reference passes with zero warnings and zero errors. The private worktree's six intermediate obj files were updated by Godot SDK because `IntermediateOutputPath` was not explicitly redirected; this is recorded separately from external final output. Frozen test binaries, shared deployment, root dirty state and user files were preserved. No rebuild, PCK export, engine start, cache cleanup, push or deployment was performed. Main review and original-case runtime retry remain pending; the existing failed attempt stays failed and its other three cases stay NOT_RUN.
