# issue#206：2026-10-11反馈②

基线：`origin/main@ea543eb7acd88bfd11f701713055da52329a699a`。本页为增量审计，保留上一轮历史。

## 五项反馈对应

| 反馈 | 修改 |
|---|---|
| 结束按钮悬浮轮廓仍裁切 | 按本轮要求直接取消正常结束按钮；临时选人/异常按钮仍用原生选项。读取GIF抽样确认左轮廓裁切，不以此前边距算术PASS替代视觉事实 |
| 最后一句后直接进入选项，留下最后两句 | 最后一句真实渲染/MarkDisplayed后等待1秒原生淡入与滚动及现有010/035音频结束，自动调用事务完成；成功才回原奖励页，重新用原生UI呈现TakeLast(2)，保留双方头像/尾巴，旧更早句不再挂在奖励页 |
| 奖励选项左侧被覆盖 | ResetSize后同时将ContentContainer左右offset对称、按内容最小宽+64扩展裁切宽，并明确重算Content横坐标；阅读/选人与原奖励回流两条路径都执行，不关闭纵向裁切、不裁掉原轮廓 |
| 涅奥声音改到最后，检查其他先古 | 从对应原版DialogueSet.GetAllDialogues声明中取原生NPC音效，不抽RNG/不猜新资源路径；仅新一次末句成功提交后Close播放，跳过/保存失败/同房已完成读档不补播 |
| 重启旧局无需sgd立刻启用 | OnSetupComplete按当前资格独立Get/Attach，不依赖原版可省略的SetDialogue调用；未完成的有效房间立即Begin，首次事务初始化固定v1.3.0 epoch，从共同初见开始；不重复清空已获进度，不重开已领取终结事件 |

## 原版109调用审计

- CodeGraph定位NEventRoom.SetupLayout、NAncientEventLayout.SetDialogueLineAndAnimate、NAncientDialogueLine.PlaySfx，定向回读实际定义/场景；两个游戏源码目录只读。
- SetupLayout仅在古代事件且非pre-finished时SetDialogue，但始终调用OnSetupComplete。这就是不能把SetDialogue当唯一接管初始化入口的原因。
- 原生ContentContainer是中心锚定裁切区域；Content容器Resize后的旧X不会被只改Y的SetDialogueLineAndAnimate纠正。本次明确重算clipper与Content中心，并保留原来的垂直滚动/旧句淡出。
- 原版Neow、Darv、Nonupeipe、Tanx均声明`event:/sfx/npcs/`语音（首个分别welcome/introduction/welcome/laugh）；Orobas、Pael、Tezcatara、Vakuu的对白只给空路径，原版回退UI enchant，不凭空新增NPC录音。建筑师不按AncientEventModel取声音。
- 先古背景AmbientBgm循环与各句通用UI提示音不改；原版声音仍走SfxCmd/FM​​OD，不受盖塔010/035消费掩码影响。这里只把声明的NPC声音安排在本次末句保存成功后。

## 所有权与事务

- 原奖励按钮继续保留原实例；Bridge恢复暂停前已启用且仍有效/属于原布局的按钮，然后原resume优先。不伪造选择，不全局Enable、不增加领奖或建筑师攻击/评分消费者。
- 延迟奖励布局操作核验节点存活及原选项实例序列，页面已换/节点释放则退出；完成之后允许原生SetDialogue绘制保留的尾句，不再递归接管。
- 保存失败不完成、不播放NPC声音，继续原生错误页重试/异常跳过；重试只提交一次。同房已关闭恢复不重播声音、不重新抽对白。未知future epoch/损坏存档/原子备份保全仍保持。
- 正常页无Back/ui_cancel/Finish按钮；建筑师中间句保留原生继续，末句也自动完成。仍只有010/035两句盖塔语音，无动作演出。

## 开发自测与边界

- 新反馈门禁在基线先RED全部6条契约，修正GREEN；新8个反向突变拒绝。原6份206门禁及4/9/11旧反向组仍PASS，共7份validate、4组mutation。
- 正式109引用build 0 warning/0 error；生产源码Console/进度919断言、Bridge 63断言PASS。新增覆盖：无sgd冷初始化、未显示/中间句不完成、完成写锁失败回滚、自动完成仅一次、同房已闭合恢复与epoch不重复清零；以及不经SetDialogue的冷接管、排除已结束/不合格事件、复用/缺失NPC音效路径。
- 临时环境与UI为managed stand-ins，未启动Godot/游戏、未验证物理输入/实际排版/FM​​OD听感，未PCK/初始化/部署/PR/合并/发布。不把静态/算术/夹具PASS当作反馈图中的裁切已人工验收。
- 不改119套对白、63段三语文本、图像/声音素材、主开发PCK工具、根用户状态及共享部署；完成后交主开发独立审核。
