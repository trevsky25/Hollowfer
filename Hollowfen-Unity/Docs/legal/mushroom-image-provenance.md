# Mushroom Image Provenance Register

Last verified: 2026-07-17

## Scope and evidence

This register covers the 16 PNG files in `Assets/_Hollowfen/UI/Mushrooms`. Each file is a 1254 x 1254 RGB PNG. Readable fields in each file's embedded C2PA/JUMBF metadata report:

- action: `c2pa.created` on `2026-04-28T00:00:00Z`;
- software agent: `gpt-image`, version `pre-2.0`;
- digital source type: `trainedAlgorithmicMedia`;
- claim generator: `OpenAI Media Service API`; and
- signer identity strings: `OpenAI OpCo, LLC` / `OpenAI Media Service`.

The repository does not include a C2PA verification tool, so this audit inspected the embedded fields but did not independently validate the manifest's cryptographic signature. These fields establish the files' claimed generation origin; they do not by themselves prove a license or ownership chain.

## Asset inventory

All rights-evidence entries below remain **unresolved**: the repository contains no generation receipt or job ID, source prompt/export record, account record, or snapshot/reference for the terms that applied when the images were generated.

| PNG asset | Referenced by mushroom ID(s) | SHA-256 |
| --- | --- | --- |
| `bonepale.png` | `bonepale` | `f4b092dcbbb6a9c50d45aab025553246c05e15924bb1bef4f0b022008ba3dccd` |
| `brightspore.png` | `brightspore` | `d70c747dce0d0a80feae7e899d587dfdfa8b77aec3f54d97d35769c3321d8d15` |
| `chanterelle.png` | `chanterelle` | `52dfc2bd42a9d1e92ca7e37e957c425826c02cba33cc701ed45d66dbca48d6c8` |
| `coppercup.png` | `coppercup` | `da6282b7cd84fe9598b03ed9eee2ea36e33402894a1494340168873b4dd3340e` |
| `deadly_galerina.png` | `deadlyGalerina` | `35b66e08e95d31946be8eee2709ac63d61fd74a1268fc18a0e9a05d1634d370c` |
| `death_cap.png` | `deathCap`, `hollowheart` | `47fffcb41ca62e9c4c44454161181711e57a6c16a9a0e72dbf972968ba881577` |
| `destroying_angel.png` | `destroyingAngel`, `moonring` | `e78ecd913f0589cbad436e8f4e831dfab7b52c30d8b927afb6a77f505c2a0332` |
| `field_cap.png` | `fieldCap` | `484246499b7efe92ecf03590ba064853d3d9c35edfeaeec9804c3cb63dedb957` |
| `field_mushroom.png` | `fieldMushroom` | `b00271f5ed295c8664ebfdc4f0a6935ba504f3d7c63d7f93631ce3e9db8b85dd` |
| `fly_agaric.png` | `flyAgaric` | `8845f6d2a79f698a7221431f8259586206e9d8612135267c11433eebb294515c` |
| `goldfoot.png` | `goldfoot` | `03069adb3d0757453941e4e5349df8c42b0c44b958af366fafcac49f9b24cb81` |
| `lacewig.png` | `lacewig` | `b4cbd7b86c9aa0e08f50cf910802e215d0017cd563459caa2fe3633b73262c4e` |
| `liberty_cap.png` | `libertyCap`, `wendlight` | `337a42c6df614d11ee249c5644e9d6c79c2cec92e00565cf5d10de980b5ddd69` |
| `pinecrest.png` | `pinecrest` | `1f022c697a7dbe345980e5b9944ba76149832f94e17b6d1f3f2e57d1b7edd3db` |
| `porcini.png` | `porcini` | `ad6c66622b4c6de45cccbc9527db24bbbee942dbf833b70df45bd3169d51a4b4` |
| `wood_ear.png` | `woodEar` | `ba739f61026c1b9c3751accd3378a4b9aa94d711ccae1a77f1452fa538ea0c6b` |

`oyster` and `aldermark` currently reference no field-guide PNG and therefore are not included in the image inventory.

## Shipping follow-up

Before release, archive evidence connecting these files to the developer's generation account and the applicable OpenAI terms, and disclose the shipped pre-generated AI images accurately in Steam's Content Survey. Do not describe an image as Wikimedia Commons or Creative Commons content unless its exact source URL, creator, license name/version, and required attribution are recorded here.
