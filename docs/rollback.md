# 回滚

改版前保留的完整版本：`artifacts/backups/mouse-baseline-20261012-043453`，包含 source、server、addon 和文件清单。该版本是此前认可的鼠标轮盘，四项根目录；完整回滚也会回退新功能。

保留新功能而切换透明队伍色外观：主配置设 `ClassicStyle=false`，服务端执行 `sw_buywheel_reload`。

完整回滚，在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/restore-baseline.ps1 -BackupPath 'artifacts/backups/mouse-baseline-20261012-043453'
```

脚本先把当前已知源码目录和产物移至新的 `artifacts/backups/before-restore-*`，随后复制原备份。先移出目录可避免新旧源文件混用；不会删除备份、修改旧插件或自动替换正在运行的服务器/客户端。

需要实际恢复游戏测试版本时，先停止目标测试服务器，部署备份 server 中的插件与对应旧配置；备份 addon 中的两个编译文件分别复制到本地服务端/客户端的同名资源路径，重启客户端。可再次从 before-restore 归档恢复当前版本。客户端资源必须与对应 DLL 的布局一致。

测试环境的框架 core.jsonc 原文件备份位于 `.local/test-env.local.json` 的 CoreBackup，资源是否原先存在和原文件备份也记录于该清单。测试清理由 `scripts/test/restore-local.ps1` 执行；默认保留新插件和 UI，恢复框架原配置并移出测试探针。
