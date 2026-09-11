# Player Banking Sprite QA

- `PlayerIdle.png`, `PlayerBankingLeft.png`, and `PlayerBankingRight.png` are separate 16x16 transparent sprites derived from one ship identity.
- Idle is symmetric; Banking variants use opposing wing foreshortening and lighting while retaining the same center anchor.
- Runtime target: Sprite (Single), PPU 16, Point filtering, mipmaps disabled, uncompressed.
- AnimationPack mapping remains Idle ID 0, left Banking ID 1, right Banking ID 2.
- Banking is presentation-only; movement speed, direction, body size, and `DamageRect` are unchanged.
