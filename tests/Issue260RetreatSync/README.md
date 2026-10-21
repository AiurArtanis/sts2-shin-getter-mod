# issue#260 战术撤退同步定向CPU回归

从仓库根目录运行：

```powershell
dotnet run --project tests/Issue260RetreatSync/Issue260RetreatSync.csproj --no-launch-profile
```

测试项目直接链接5份生产源码：实际撤退入口、SwitchTo、战术变形、时间线/上下文、Tween等待器。时间线使用非均匀帧时长、不同FPS与速度，不用固定帧数代替真实数据。覆盖三个原子形态循环、缺前形态、非原子/缺资源fallback、同creature嵌套与异步owner隔离、提前取消、节点退出、外部Kill、异步异常、位置/目标可见性/Idle恢复、普通SwitchTo分流及无视觉变形。

Godot对象、Tween/信号、SpriteFrames与资源可用性、Block动作等待、原普通fusion实现、残影和游戏单例使用CPU替身。普通路径检查只证明SwitchTo仍分流到原函数；不证明原函数的原生表现。fallback检查调用真实SwitchTo与等待器，Tween属性渲染由替身承接。

取消分流：仅视觉Tween被Kill或Owner显示节点退出，而战斗仍活跃且角色存活时，结束视觉并保留Transform后续模型链；战斗结束/Owner死亡保留动作取消，非取消异常仍传播。不重放已经开始的Transform。CPU用模型尾部计数检查是否继续一次，不把该计数称为真实卡牌格挡/形态结算验收。

这些检查不加载Godot原生库，不启动游戏、渲染、GPU、PCK导出或音频。它们验证源码控制流、数据驱动计时与清理，不能替代实机同帧/视觉/录制验收，也不表示整体v1.3.0回归通过。

主开发仍需在正式目标录制候选中检查：撤退结束后的视觉同起同止；实际帧展示与位置/Idle；所有形态与fallback；取消/场景释放；卡牌两次格挡/分身/形态效果和语音机制保持；动画和结算结束后再衔接下一句旁白。
