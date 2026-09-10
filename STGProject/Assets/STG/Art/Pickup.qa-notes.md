# Pickup sprite QA

- State: `idle`; verdict: pass.
- Four requested frames extracted; atlas and frame manifests report `ok: true`.
- Motion reads as a subtle center-light pulse with a stable silhouette and acceptable loop seam at 10 fps.
- No chroma-adjacent pixels remain in any extracted frame.
- The source model rendered a finer grid than requested; pixel-unfake normalized the final runtime cells to 16x16 with a shared 16-color palette.
- Source run: `C:/Users/JIM/AppData/Local/Temp/STGPickupSpriteRun-20260911-01`.
