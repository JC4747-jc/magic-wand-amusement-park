# Standing statue stretch candidate

This folder is intentionally outside Unity Assets. It does not replace the shipped marble bust or bronze variant and is not included in the APK.

- `Superhero_Male_FullBody.fbx`: unmodified male source from Quaternius Universal Base Characters [Standard], Unity directory, free CC0 package.
- `LICENSE.txt`: original package license.
- `StandingStatue.fbx`: derived fictional sculpture with authored pelvis-weighted waist drape and one 5.067-second overhead stretch.
- `provenance.json`: source, content hashes and candidate status.

Official sources: https://quaternius.com/packs/universalbasecharacters.html and https://quaternius.itch.io/universal-base-characters . This is a superhero body base, not a historical scan or the paid Regular character.

Builder: `Tools/prepare_stretch_statue.py`, Blender 4.5.3. Import fixture: `Tools/Unity/StatueStretchAssetChecks.cs`, Unity 6000.0.33f1. Evidence and limitations: `Reports/hci-statue-stretch-20261116/README.md`.

The stone shader's Blender procedural bump is not transferred by FBX. The Unity check supplies a simple URP/Lit material. Production gesture integration, Play Mode lifecycle, final art polish and PICO performance remain pending.
