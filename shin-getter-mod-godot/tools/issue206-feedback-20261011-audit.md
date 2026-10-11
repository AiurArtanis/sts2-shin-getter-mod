# issue#206：2026-10-11反馈修正

基线：`origin/main@e1ce7861db64e568d4f7d0cf8a33d71ac5472db0`。本页是2026-10-10原生UI审计的增量修正；不改写旧审计的历史证据。

## 反馈与修改对应

| Artanis反馈 | 原因 | 修正 |
|---|---|---|
| 不需要跳过对话的返回按钮 | 场景适配器额外挂载了NBackButton，并注册ui_cancel直接Skip | 正常页移除节点、订阅、热键、焦点候选及取消键入口；仅保存失败/资源异常提供退出恢复 |
| 结束交谈按钮悬浮超出边界 | 临时原版按钮未为悬浮Outline预留容器空间 | 每个临时原版按钮由MarginContainer预留横向32/纵向24空间；保留原生1.01悬浮、0.99按压及Outline，不靠裁切掩盖问题 |
| 个人分支只有姓名用专属色，冒号与正文用正常色 | NEventOptionButton._Ready会把非空Title全文包在gold/b中 | 仅个人分支用空Title和完整Description；已有姓名标签原样使用，操作按钮仍用原生标题格式 |

## 正式109契约核对

- 先CodeGraph定位`NEventOptionButton._Ready/OnFocus/OnPress`和`NAncientEventLayout.SetDialogueLineAndAnimate`，再回读原版两个选项场景。游戏源码不修改。
- 古代选项根min为1000×100，根pivot=(450,50)，Outline左右偏移±12、Y范围[-13,111]、scale=(1.005,1.05)、pivot=(400,50)。将Outline和父按钮两级变换计算后，idle/press/hover均落入32/24预留；门禁锁定这些原生参数，变动后必须重新审计。这是布局算术，不是渲染截图验收。
- MarginContainer无输入和焦点，仅原生按钮加入循环焦点。选项树加入后原生_Ready完成，保留鼠标Released路径及精确EventOption本地拦截。
- 清理先Disable按钮，再同步移出并QueueFree整组inset/子按钮，清空两份引用；正常结束/错误/语言切换/场景退出不保留失效临时焦点。
- 原奖励按钮原实例保留，返回时原有父节点判定不改；Bridge、Session、存档epoch、两句010/035语音和全部119套对白/63段三语正文均不改。

## 开发自测

- RED：新增`validate_issue_206_feedback_20261011.py`在基线同时报告normal skip、个人选项格式和hover reserve三项失败。
- GREEN：正式109引用build 0 warning/0 error。sts2.dll SHA-256：`C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0`。
- 6份206定向validate：core、console、event-return、native-ui、release-reset、20261011 feedback；3组突变测试：priority拒绝4个回退，native-ui拒绝9个，20261011拒绝11个；正式109输入/标题/布局源码探针PASS。
- 三语189条个人选项全部限定姓名标签，正文无整句颜色覆盖。56份tracked JSON/BAD=0，diff-check PASS。
- Issue206Console：901条链接生产源码断言；Issue206EventReturn：52条生产Bridge断言，均PASS。使用隔离临时目录，不读写玩家存档。
- Impeccable布局机械扫描无报告；该扫描不验证Godot渲染，本轮视觉仍待人工确认。

## 验证边界

本轮未运行Godot/游戏自动化、PCK/初始化/共享部署/PR/合并/发布，不把静态、布局算术及managed夹具升级为实际输入、视觉或语音验收。与主开发并行打包工具改动隔离，不改pack工具、源素材或游戏源码。交付后由主开发独立审核及统一集成。
