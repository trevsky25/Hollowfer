# Performance Benchmarks

Measured regression references for Hollowfen. These numbers are only comparable when the method, route,
resolution, Unity version, and sample counts match. They do not replace a profiled release player on target
hardware.

Status: baseline established 2026-07-21 in Batch 120 with Unity 6000.4.4f1 and Unity CLI 1.0.0-beta.2.

---

## Batch 120 — village Editor diagnostic baseline

Command: `python3 tools/agent/capture_visual_baseline.py --samples-per-stop 6 --timed-frames 60`

Game View was fixed at 1280×800. Each stop received 45 settling steps, 60 wall-time samples around
`EditorApplication.Step()`, and six native Pipeline performance snapshots. The timing is CPU-side and includes
Editor overhead; triangle and SetPass values describe the fixed camera presentation at each coordinate. Peak
allocated memory is Unity's total-allocated counter during that stop, not a leak measurement.

| Fixed stop | Position / yaw | Step median | Step p95 | Step max | Median triangles | Median SetPass | Peak allocated |
|---|---:|---:|---:|---:|---:|---:|---:|
| Crooked Pintle | 275, 88 / 5° | 4.68 ms | 5.13 ms | 5.71 ms | 3,475,969 | 109 | 6.86 GiB |
| Village well | 286, 160 / 0° | 5.86 ms | 6.33 ms | 6.51 ms | 6,767,482 | 147 | 6.86 GiB |
| Theo market road | 325, 213 / 285° | 4.87 ms | 5.13 ms | 5.61 ms | 3,788,609 | 112 | 6.86 GiB |
| Joren forge | 198, 198 / 315° | 3.88 ms | 4.28 ms | 4.34 ms | 2,841,381 | 80 | 6.86 GiB |
| Tobin mill | 233, 318 / 180° | 3.55 ms | 3.91 ms | 4.20 ms | 396,708 | 79 | 6.86 GiB |

Machine-readable evidence, including the exact coordinates and raw gate summaries, is in
`Docs/screenshots/batch-120/baseline-report.json`. The companion folder contains the eight visually reviewed
1280×800 UI baselines.

### Interpretation boundary

- Use these values to flag meaningful deltas after scene dressing, material, mesh, texture, URP, or hot-path changes.
- Do not convert Editor step time into a standalone framerate claim; it omits target-GPU frame timing and includes Editor work.
- Steam Deck's village floor remains 60fps and still requires a non-development release build profiled on Deck hardware.
- A single higher sample is a prompt to rerun under matched conditions, not proof of a regression.
