# Boss sprite QA

- Center and wing canonical bases passed the base-lock gate as complete, single-component, hard-edged pixel sprites on `#00FF00` chroma.
- States `operational` and `damaged` each contain four requested frames at 10 fps; both atlas reports and frame manifests report `ok: true`.
- Manual motion review passes: operational rows use a readable light pulse; damaged rows retain a stable wreck silhouette with attached spark flicker and an acceptable loop seam.
- Automated inspection reports low-motion warnings for the deliberately subtle damaged loops and pixel-pitch normalization warnings; neither row is empty, cropped, chroma-contaminated, or structurally inconsistent.
- Runtime atlases use deterministic 32x32 center cells and 24x24 wing cells with a shared 16-color pixel-unfake palette per run.
- Source runs: `C:/Users/JIM/AppData/Local/Temp/STGBossCenterSpriteRun-20260912-01` and `C:/Users/JIM/AppData/Local/Temp/STGBossWingSpriteRun-20260912-01`.
