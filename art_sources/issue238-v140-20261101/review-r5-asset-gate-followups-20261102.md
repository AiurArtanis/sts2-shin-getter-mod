# issue#238 R5 三项P2门禁修正

主开发对`8cc0c2bd`产品保存源码和实际资源已准入最小运行，同时指出三处工具漏验。自动化封存并校验8cc精确输入后，本补丁仅修改资源门禁、负向用例、必要来源凭据及本说明；不改产品src或运行时资源，不编译、不启动游戏。

## 修正

1. 固定4个物品ModelId、唯一slot及3遗物/1药水类型映射，强制原生icon/outline/big路径；精确解析atlas引用、区域及每一处filter_clip。注释中的同名字符串不能代替实际属性。
2. 独立固定可移植凭据SHA，而非只检查mutable manifest自述。凭据完整包含9件批准、5卡导出、4物品导出、两张物品atlas与9个非空job对应记录。实际读取9件批准源，逐个与批准源SHA及固定模型/job绑定；正式大图和atlas文件SHA、各运行时小图RGBA与导出凭据交叉。原始批准/导出收据的三个SHA保留供复算，空jobs、零SHA、伪造自洽manifest均拒绝。
3. 卡牌atlas从读取起使用RGBA，并对五个允许槽以外逐字节比较；不丢alpha，也不依赖裸RGBA getbbox。图标/轮廓8处均要求唯一有效filter_clip=true。

只新增两份此前未入库的批准母图：钻头RGB 1,156,520 bytes、机动催化剂256母图55,507 bytes；其余7件批准源复用现有正式资源。不复制批量原图或独立小图。缩略图与图标的RGBA摘要从原导出PNG计算，该PNG的SHA先与原始导出收据核对；运行时atlas实际区块须一致。

可移植凭据SHA：`60636b8d533a299e82ceb1759d4284605f7ed87416d800873dcb588c68057f5e`。凭据按仓库`.gitattributes`保存为UTF-8/LF，工作文件与Git blob逐字节一致；原始外部收据SHA按原始字节保留。

## 自检

- 正常资源门禁PASS。
- 31个内存负向全部拒绝，执行前后正常资源均PASS；未写坏任何fixture、产品或批准源。
- 包含四份蓝宝石、重复/互换slot、错误原生入口/物品类型、零源SHA+空jobs、空/重复/缺项/改SHA的job收据、伪造批准/导出/可移植收据、实际源缺失/变更、实际大图/物品atlas变更后同步更新manifest、槽外alpha-only和RGB-only变更、八处逐一/全部裁切关闭及注释伪装。
- 产品源码与运行时资源相对`8cc0c2bd`无差异。新原图只位于Godot工程外的art_sources，不进入res://或本轮封存PCK。

```text
python -X utf8 -B shin-getter-mod-godot/tools/validate_issue_238_assets.py
python -X utf8 -B shin-getter-mod-godot/tools/test_issue238_assets_gate.py
git diff --check
```

这是工具缺口修复，不能把旧生产保存FAIL改为通过。真实保存/Continue、人审两篇剧情、旧资源/生命周期边界继续由对应证据独立裁定。未PR、合并、部署或发布。
