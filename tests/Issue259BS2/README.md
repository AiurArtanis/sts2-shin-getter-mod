# issue#259 冷启动读档安装门禁

`test_startup_paths.py`只读取生产补丁和指定游戏源码，检查新局、单人读档、多人读档各自直接经过安装入口，且读档时安装早于`InitializeSavedRun`。109与111的方法大小写差异由测试匹配处理，不修改游戏源码。

```powershell
python tests/Issue259BS2/test_startup_paths.py --run-manager '<109源码>/src/Core/Runs/RunManager.cs' --run-manager '<111源码>/src/Core/Runs/RunManager.cs' -v
```

每个读档检查从自己的`SetUpSaved*`入口开始，不假设当前进程已经开过新局或执行过`SetActInternal`。使用`--patch-source`传入被退回的`621dad43`补丁副本应失败，证明旧安装入口不能通过门禁。

这些是静态调用接线门禁，不执行Harmony、BS2或游戏，也不证明候选池运行时生效。主开发仍须在对应111组合产物上执行：

- 全新进程直接继续第二章SmokeForest单人存档，之前不进入任何新局：首次探索前适配已安装，符合条件时探索候选池增加曼陀罗。
- 另一个全新进程直接恢复对应多人存档，同样不先开新局：各端安装一致，候选及抽取一致。
- 分别验证BS2先加载和真盖塔先加载；进入后续章节或同进程再读档不重复安装；未安装BS2时正常跳过。

保留上一轮章节、角色、已访问、候选不重复及原随机/结算行为验收。本次修订只更换安装时机。
