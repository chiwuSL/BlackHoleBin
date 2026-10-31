# Security / 安全说明

This is a local desktop utility, not a secure-erasure tool or encrypted vault.

- The application has no intentional networking or telemetry. Screen samples remain in memory; the production app does not save screenshots.
- Recovery copies are plaintext local files. Same-account software and administrators may read them. Clearing the vault permanently removes those copies.
- Path validation is not an atomic filesystem transaction; malicious same-user path races remain outside the verified guarantees.
- NTFS metadata preservation is not comprehensively tested. Undo and vault copies do not replace independent backups.
- Binaries are not Authenticode-signed. Do not mix in DLLs from untrusted sources.

See [the 1.0.0 source audit](docs/SECURITY-AUDIT-1.0.zh-CN.md) for scope, tests and limitations.

本程序是本地回收工具，不是安全擦除软件或加密保险箱。保险副本明文保存；请为重要数据保留独立备份。详细检查范围、已修复问题和剩余边界见上述审查报告。

If reporting a vulnerability, do not post credentials, private screenshots or original personal file paths in public issues. Use synthetic reproduction data.
