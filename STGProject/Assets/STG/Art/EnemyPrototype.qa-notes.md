# Enemy Prototype Sprite QA

- `EnemyStraight.png`: 16x16, 91 visible pixels, transparent background; compact blue pointed silhouette.
- `EnemyShooter.png`: 24x24, 288 visible pixels, transparent background; wider warm-accented shooter silhouette.
- `EnemyBullet.png`: 8x8, 39 visible pixels, transparent background; orange-red core distinct from the cyan player weapon.
- Runtime import target: Sprite (Single), PPU 16, Point filtering, mipmaps disabled, uncompressed.
- Collision remains authored separately through serialized damage rectangles; no `Collider2D` is used.
