# 调研记录

查询日期：2026-09-26

当前仓库没有 package.json、src/ 或 electron/；AGENTS.md 记载的 MVP 不在当前工作树，不能当作已实现能力。

## GLB 结构检查
```json
{
  "asset": {
    "generator": "Tripo",
    "version": "2.0"
  },
  "bytes": 16436932,
  "nodes": [
    {
      "mesh": 0,
      "name": "tripo_node_65c7ffdc-f698-4fcc-82da-f23122939359"
    }
  ],
  "skins": [],
  "animations": [],
  "mesh_count": 1,
  "primitives": [
    {
      "attributes": {
        "POSITION": 0,
        "NORMAL": 1,
        "TEXCOORD_0": 2
      },
      "vertices": 284228,
      "indices": 1504272,
      "targets": 0
    }
  ],
  "materials": [
    {
      "name": "tripo_mat_65c7ffdc-f698-4fcc-82da-f23122939359",
      "normalTexture": {
        "index": 0
      },
      "pbrMetallicRoughness": {
        "baseColorTexture": {
          "index": 1
        },
        "metallicRoughnessTexture": {
          "index": 2
        }
      }
    }
  ],
  "images": [
    {
      "bufferView": 4,
      "mimeType": "image/jpeg",
      "name": "nookin-tripo-v1_normal"
    },
    {
      "bufferView": 5,
      "mimeType": "image/jpeg",
      "name": "nookin-tripo-v1_basecolor"
    },
    {
      "bufferView": 6,
      "mimeType": "image/jpeg",
      "name": "nookin-tripo-v1_rm"
    }
  ],
  "extensionsUsed": null
}
```

## 用户偏好
尽量复刻 MateEngine，重点丰富动作与桌面互动。

## 本地结论
GLB 约 15.68 MiB，284228 顶点、501424 三角面（索引数/3），单节点单网格；无 skin、animation、morph target，无法直接套用骨骼动作。pet.png 为透明背景卡通角色参考。尚未进行 GLB 实际渲染与动画测试。

## 一手产品页面
MateEngine Steam 本次页面价格 $5.49（币种符号按页面，非国区承诺）；Windows 11，Windows 10 未测试；音乐跳舞、触摸、VRM、窗口停靠、聊天。窗口停靠同时被标注实验性与已禁用，VRM1 也有支持/即将支持矛盾，必须实测。README 自制比较表不可作独立竞品测评。
Desktop Mate 基础免费，示例 DLC $14.99；当前已有 macOS 标签并明确不支持 Intel Mac；多角色仍标 beta。不要沿用旧版只支持 Windows 的结论。
来源：https://store.steampowered.com/app/3625270/MateEngine/ ; https://store.steampowered.com/app/3301060/Desktop_Mate/?l=schinese ; https://github.com/shinyflvre/Mate-Engine

## 技术与授权关键结论
MateEngine 固定读取 commit 2c5ea6b8f4cf5e1773a0816b46d9267cda5174d4，Unity 6000.2.6f2。AvatarWindowHandler.Update 在非 Windows 平台直接返回；鼠标追踪使用 Humanoid 骨骼与分层权重。LICENSE.md 开头为 MateEngine Pro License v2.1，包含禁止商用及限制发布平台条款，README 又称混合 AGPL/MatePro，不能按宽松开源直接 fork 商业产品。
UniWindowController 可处理 macOS/Windows 自身透明窗口和穿透，不负责其他应用窗口识别；文档注明不支持多窗口、部分稳定性未完全测试。
四张 PNG 均已目视检查：同一角色的不同全身姿态，不是分层动画素材。GLB 三张纹理均 2048x2048，三角形模式为 4，面数计算确认。
