# issue#206 CPU事件回流夹具

```powershell
dotnet run --project tests/Issue206EventReturn/Issue206EventReturn.csproj -p:NativeSourceRoot=E:/Work/SlaytheSpare2
```

也可设置`STS2_SOURCE_ROOT`。源目录只读；测试不启动游戏、Godot或GPU。

生产Bridge原样链接。`link_native_semantics.py`从指定正式109源码抽取`EventModel.SetEventState`与`NEventRoom.SetOptions`真实方法体到本项目忽略的`obj/`目录，参与CPU编译；不把游戏源码提交进仓库。唯一语法适配是把反编译生成的单元素集合包装替换为数组。构建时输出两个方法体SHA256。其余引擎、UI、输入和结局消费者仍是托管环境替身，不可据此称原生点击／结局验收通过。

夹具特别执行“空model选项→永久IsFinished→native SetOptions替换成地图Proceed”的负向对照。修复后的异常回退必须保留model未结束状态，异步首句期间不允许原生点击，结束后恢复；null／空／抛异常旧对白必须有可供原生WinRun读取的非null结局载荷。正常native逐句选项不能被应急结局覆盖。

用旧Bridge验证RED（绝对路径指向临时副本）：

```powershell
dotnet run --project tests/Issue206EventReturn/Issue206EventReturn.csproj -p:NativeSourceRoot=E:/Work/SlaytheSpare2 -p:BondBridgeSource=C:/Temp/old-68669e07-Bridge.cs
```

该旧候选应报`P1: Architect legacy intro permanently finished model; native SetOptions substituted map Proceed`。重新运行当前Bridge必须通过。不能把测试中的托管`Chosen`调用或计数当成viewport鼠标或真实攻击／评分证据。
