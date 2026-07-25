# VRChat OSCQuery 参考文档

## 概述

OSCQuery 是 VRChat 用于查询和控制 OSC 状态的 HTTP 服务。它提供了 RESTful API，可以：
- 查询当前 VRChat 状态
- 获取可用参数列表
- 读取/设置参数值

## 端口发现

VRChat 的 OSCQuery 服务端口是**动态分配**的，不是固定的 9001。

### 获取端口方法

1. **通过进程 TCP 端口枚举**（推荐）
   - 获取 VRChat.exe 进程 PID
   - 使用 Windows API `GetExtendedTcpTable` 查询该进程的所有 TCP LISTENING 端口
   - 对每个端口尝试 HTTP GET 请求
   - 响应中包含 `FULL_PATH` 即为 OSCQuery 服务

2. **扫描常见端口范围**
   - VRChat 通常使用 13000-14000 范围内的端口
   - 不推荐，效率低且不可靠

## API 端点

### 基础端点

| 端点 | 说明 |
|------|------|
| `/` | 根节点，返回完整参数树 |
| `/input/` | 输入参数 |
| `/avatar/parameters/` | Avatar 参数 |
| `/tracking/` | 追踪数据 |
| `/chatbox/` | 聊天框相关 |

### 麦克风控制

| 端点 | 说明 | 类型 | 值 |
|------|------|------|-----|
| `/input/Voice` | 麦克风开关 | `T` (bool) | `true` = 未静音, `false` = 静音 |

**响应示例：**
```json
{
  "DESCRIPTION": "In 'Toggle Mic' mode, Toggles your Mic when changing the input from 0 to 1. In Push-To-Talk Mode, 0 = Muted and 1 = Unmuted.",
  "FULL_PATH": "/input/Voice",
  "ACCESS": 2,
  "TYPE": "T",
  "VALUE": [false]
}
```

### Avatar 参数

| 端点 | 说明 | 类型 |
|------|------|------|
| `/avatar/parameters/MuteSelf` | 自己是否静音 | `T` |
| `/avatar/parameters/Voice` | 语音输入音量 | `f` (float) |
| `/avatar/parameters/AFK` | AFK 状态 | `T` |
| `/avatar/parameters/Grounded` | 是否在地面 | `T` |
| `/avatar/parameters/VRMode` | VR 模式 | `i` (int) |
| `/avatar/parameters/TrackingType` | 追踪类型 | `i` (int) |
| `/avatar/parameters/Viseme` | 口型同步 | `i` (int) |
| `/avatar/parameters/GestureLeft` | 左手手势 | `i` (int) |
| `/avatar/parameters/GestureRight` | 右手手势 | `i` (int) |

### 输入参数

| 端点 | 说明 | 类型 |
|------|------|------|
| `/input/MoveForward` | 向前移动 | `T` |
| `/input/MoveBackward` | 向后移动 | `T` |
| `/input/Jump` | 跳跃 | `T` |
| `/input/Run` | 奔跑 | `T` |
| `/input/UseRight` | 使用右手物品 | `T` |
| `/input/GrabRight` | 抓取右手物品 | `T` |
| `/input/DropRight` | 丢弃右手物品 | `T` |
| `/input/PanicButton` | 紧急按钮 | `T` |
| `/input/AFKToggle` | AFK 切换 | `T` |

### 追踪数据

| 端点 | 说明 |
|------|------|
| `/tracking/trackers/head` | 头部追踪器 |
| `/tracking/trackers/1` ~ `/tracking/trackers/8` | 身体追踪器 |
| `/tracking/eye/` | 眼球追踪 |
| `/tracking/vrsystem/` | VR 系统追踪 |

## 数据类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `T` | Bool | `[true]`, `[false]` |
| `f` | Float | `[0.5]`, `[1.0]` |
| `i` | Int | `[0]`, `[1]` |
| `s` | String | `["hello"]` |
| `ff` | 多个 Float | `[0.1, 0.2]` |
| `fff` | 三个 Float | `[1.0, 2.0, 3.0]` |

## ACCESS 权限

| 值 | 说明 |
|----|------|
| 0 | 容器（无值） |
| 1 | 只读 |
| 2 | 读写 |
| 3 | 读写（隐藏/内部） |

## HTTP 请求格式

### 请求示例

```
GET /input/Voice HTTP/1.0
```

### 响应格式

```
HTTP/1.1 200 OK
Content-Type: application/json
Content-Length: 123

{JSON 数据}
```

## 实现注意事项

### 端口探测

由于端口动态分配，需要：
1. 获取 VRChat 进程 PID
2. 枚举该进程的所有 TCP LISTENING 端口
3. 对每个端口发送 HTTP GET 请求
4. 检查响应是否包含 `FULL_PATH` 字段

### 性能优化

- 使用原始 `TcpClient` 而非 `HttpWebRequest`
- 只读取前 512 字节即可判断是否为 OSCQuery
- 连接超时设为 300ms，读取超时设为 500ms
- 缓存端口，避免重复探测

### 状态同步

- 启动时查询一次 `/input/Voice` 获取初始状态
- 后续通过 UDP 监听 `/input/Voice` 变化实时更新

## 相关资源

- [VRChat OSC 官方文档](https://docs.vrchat.com/docs/osc)
- [OSCQuery 规范](https://vrcoscquery.com/)
