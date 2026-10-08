# EFCore.BulkOperations Benchmark Protocol

Automated benchmark results comparing Classic EF Core against EFCore.BulkOperations (1,000,000 synthetic records).

| Date (UTC) | Provider | Mode | Records | Classic Insert | Bulk Insert | Insert Speedup | Classic Update | Bulk Update | Update Speedup | Classic Delete | Bulk Delete | Delete Speedup | Classic Total | Bulk Total | Total Speedup |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 2026-10-08 07:59:09 | SQLite | Sync | 1,000,000 | 19.96 s (50,099 rec/s) | 3.89 s (256,752 rec/s) | 5.1x | 26.67 s (37,496 rec/s) | 4.66 s (214,599 rec/s) | 5.7x | 15.08 s (66,292 rec/s) | 1.63 s (614,421 rec/s) | 9.3x | 62.06 s | 10.68 s | 5.8x |
| 2026-10-08 08:00:30 | SQLite | Async | 1,000,000 | 20.15 s (49,639 rec/s) | 3.38 s (296,189 rec/s) | 6.0x | 22.81 s (43,832 rec/s) | 3.94 s (254,005 rec/s) | 5.8x | 13.84 s (72,276 rec/s) | 1.31 s (762,959 rec/s) | 10.6x | 57.14 s | 8.85 s | 6.5x |
| 2026-10-07 20:15:27 | PostgreSQL | Sync | 1,000,000 | 34.44 s (29,034 rec/s) | 11.33 s (88,291 rec/s) | 3.0x | 44.16 s (22,644 rec/s) | 15.25 s (65,566 rec/s) | 2.9x | 27.60 s (36,231 rec/s) | 8.90 s (112,359 rec/s) | 3.1x | 106.50 s | 35.78 s | 3.0x |
| 2026-10-07 20:17:33 | PostgreSQL | Async | 1,000,000 | 35.15 s (28,448 rec/s) | 12.12 s (82,542 rec/s) | 2.9x | 45.12 s (22,163 rec/s) | 16.05 s (62,310 rec/s) | 2.8x | 28.10 s (35,587 rec/s) | 9.35 s (106,951 rec/s) | 3.0x | 108.79 s | 37.82 s | 2.9x |
| 2026-10-07 20:06:50 | MySQL | Sync | 1,000,000 | 63.45 s (15,761 rec/s) | 10.35 s (96,629 rec/s) | 6.1x | 144.27 s (6,931 rec/s) | 21.28 s (46,999 rec/s) | 6.8x | 74.80 s (13,368 rec/s) | 11.50 s (86,956 rec/s) | 6.5x | 282.83 s | 43.39 s | 6.5x |
| 2026-10-07 20:10:46 | MySQL | Async | 1,000,000 | 50.91 s (19,644 rec/s) | 7.25 s (137,872 rec/s) | 7.0x | 127.46 s (7,846 rec/s) | 16.05 s (62,321 rec/s) | 7.9x | 62.10 s (16,103 rec/s) | 8.60 s (116,279 rec/s) | 7.2x | 240.82 s | 32.17 s | 7.5x |
| 2026-10-07 20:21:41 | SQL Server | Sync | 1,000,000 | 81.09 s (12,332 rec/s) | 14.92 s (67,031 rec/s) | 5.4x | 104.47 s (9,572 rec/s) | 19.74 s (50,658 rec/s) | 5.3x | 64.50 s (15,503 rec/s) | 12.40 s (80,645 rec/s) | 5.2x | 250.49 s | 47.36 s | 5.3x |
| 2026-10-07 20:25:48 | SQL Server | Async | 1,000,000 | 83.85 s (11,927 rec/s) | 15.54 s (64,333 rec/s) | 5.4x | 103.33 s (9,678 rec/s) | 19.30 s (51,807 rec/s) | 5.4x | 66.20 s (15,105 rec/s) | 12.75 s (78,431 rec/s) | 5.2x | 253.70 s | 47.89 s | 5.3x |
