# Nookin Unity 桌宠可行性 MVP

本项目验证三件事：在 macOS 桌面显示透明置顶的角色窗口、导入用户的 Tripo GLB、用骨骼播放一个简短动作并支持点击和拖动。采用 Unity 6.3.25f1、glTFast 6.14.1、UniWindowController（提交 `304f9ba2aa4a8fae7f3c71f38118c44722a2f6cc`）。当前用 Unity 内置渲染管线，先验证窗口和交互，不引入额外渲染管线。

## 构建

在 Unity 编辑器中打开本目录，选择 **Nookin → Build macOS MVP**。macOS 命令行等价方式：

```sh
'/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity' \
  -batchmode -quit \
  -projectPath '/Users/nicolas/Projects_app/Nookin/unity/NookinMvp' \
  -executeMethod NookinMvpBuilder.BuildMacApp \
  -logFile '/Users/nicolas/Projects_app/Nookin/unity/NookinMvp/Logs/build.log'
```

输出为 `Builds/NookinMvp.app`。先前的窗口验证入口是 **Nookin → Build window probe**，输出为 `Builds/NookinWindowProbe.app`。

## 素材处理

`assets/bunny hooded kid 3d model v3.glb` 的 48 根骨骼有 inverse bind matrices，但节点缺失静止姿态变换。直接导入时 Unity 中的角色会严重变形。`Tools/repair_bind_pose.py` 从这些矩阵恢复局部骨骼变换。当前挥手版使用 `--wave-pivots` 额外把左上臂、前臂和手的旋转中心移到可见网格内，输出 `Assets/Nookin/Character/nookin-rigged-wave.glb`；脚本使用 Python 的 NumPy 和 SciPy。同目录的 `nookin-basecolor.jpg` 是原 GLB 内嵌底色贴图的提取副本。`assets/` 原件保持不变。

## 当前验证状态

- Unity 构建成功；已在 macOS 图形桌面实际打开，角色正面可显示，窗口尺寸为 460×540 渲染像素（Retina 约 230×270 桌面点）。
- Unity 日志确认窗口插件获取了原生窗口。用户在 2026-09-27 把桌宠移到普通窗口上方，确认点击桌宠窗口四角的空白区域能直接点到下方窗口。
- 当前点击回应已从点头换成约 1.35 秒的抬手、摆手、放下；待机和拖动逻辑保持原样。`Nookin → Render wave previews` 在 `Builds/WavePreviews/` 输出动作关键帧，已检查抬手时角色与袖子形状。用户此前用真实鼠标确认过点击与拖动的输入链路；新的挥手版仍需一次真人点击确认。
- 肩膀和袖子原有权重问题未修。丰富动作、AI 接入及长期稳定性均不属于这个最小验证的已通过项。

详细结果与未测项见仓库根目录的 `docs/unity-mvp-validation-2026-09-27.md`。
