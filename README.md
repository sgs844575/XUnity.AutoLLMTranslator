# XUnity.AutoLLMTranslator

## 概述 / Overview

XUnity.AutoLLMTranslator 是一个用于 XUnity.AutoTranslator 框架的插件，它通过大型语言模型（LLM）实现游戏文本翻译。

该插件提供了高性能、可定制的翻译功能，支持批次合并翻译、上下文记忆、术语替换等高级特性。

> **声明**：本项目基于 [NothingNullNull/XUnity.AutoLLMTranslator](https://github.com/NothingNullNull/XUnity.AutoLLMTranslator) 进行修改和扩展而来，感谢原作者的基础工作和贡献。

---

## 依赖 / Dependencies

### 运行时依赖 / Runtime Dependencies

| 依赖包 | 来源 | 说明 |
|--------|------|------|
| **XUnity.AutoTranslator.Plugin.Core** | 本地引用 `packages/` | XUnity.AutoTranslator 核心插件接口 |
| **XUnity.AutoTranslator.Plugin.ExtProtocol** | 本地引用 `packages/` | XUnity.AutoTranslator 扩展协议 |
| **XUnity.Common** | 本地引用 `packages/` | XUnity 公共库 |
| **Newtonsoft.Json** | NuGet `13.0.1` | JSON 序列化与反序列化 |
| **FuzzyString** | 项目内嵌 | 模糊字符串匹配，用于历史文本搜索 |

### 构建工具 / Build Tools

| 工具 | 来源 | 说明 |
|------|------|------|
| **ILRepack.MSBuild.Task** | NuGet `2.0.13` | 构建后合并程序集（MSBuild 任务） |

### 安装前置条件 / Prerequisites

1. 已安装 [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) 框架
2. 游戏运行时环境中需包含 `Newtonsoft.Json.dll`（若缺失请参考下方"可能的问题"）

---

## 特性 / Features

- **支持远程 API 及本地服务器**
- **智能批次合并翻译**：按 Token 数量（默认 384 tokens）和文本条数（默认 10 条）双重限制自动合并多个翻译请求，大幅减少 LLM 调用次数
- **完全独立于 AutoTranslator 的并发与批处理机制**：内部 HTTP 服务器并发接收请求，调度器统一合并分发
- **上下文记忆系统**：维护近期翻译、历史时间线、角色说话风格等上下文，提升翻译一致性
- **术语与字典支持**：支持自定义术语表和游戏专用字典
- ** Speaker 风格检测**：自动追踪不同角色的说话风格并在提示词中注入
- **完善的验证与重试机制**：自动检测原文返回、翻译残留、标签数量不匹配、目标语言错误等问题并自动重试
- **支持各种尺寸的 LLM**：从本地 0.6B 到云端大模型均可使用

---

## 安装 / Installation

1. 在游戏中安装 [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) 框架。
2. 将编译好的 `AutoLLMTranslator.dll` 文件复制到插件文件夹：
   - **ReiPatcher**：`<GameDir>/<GameName>_Data/Managed/Translators`
   - **BepInEx**：`<GameDir>/BepInEx/plugins/XUnity.AutoTranslator/Translators`
3. 配置 `AutoTranslatorConfig.ini` 以使用 `AutoLLMTranslate` 端点。

---

## 配置 / Configuration

### AutoTranslatorConfig.ini

```ini
[Service]
Endpoint=AutoLLMTranslate

[General]
Language=zh_cn
FromLanguage=ja
```

> **注意**：`Language` 的值会影响插件内部文件夹路径（`BepInEx/Translation/{Language}/...`）。插件启动时会自动创建所需的子文件夹。

### [AutoLLM] 配置项

| 配置项 | 必填 | 默认值 | 说明 |
|--------|------|--------|------|
| `APIKey` | 否 | `""` | LLM 服务的 API 密钥。使用本地模型（localhost/127.0.0.1/192.168.x.x）时可留空。 |
| `Model` | **是** | `gpt-4o` | 用于翻译的模型名称。 |
| `URL` | **是** | `https://api.openai.com/v1/chat/completions` | LLM 服务器的 URL。以 `/v1` 结尾会自动补全为 `/v1/chat/completions`。 |
| `Requirement` | 否 | `""` | 额外的翻译需求或指令，例如：`/no_think`（关闭思考模式）。 |
| `Terminology` | 否 | `""` | 术语表。使用 `\|` 分隔不同术语，`==` 连接原文和翻译。例如：`Lorien==罗林\|Skadi==斯卡蒂` |
| `GameName` | 否 | `A Game` | 游戏名称，帮助 AI 理解翻译场景。 |
| `GameDesc` | 否 | `""` | 游戏描述（玩法/类型/风格），帮助 AI 进行更准确的翻译。 |
| `ModelParams` | 否 | `""` | 额外的模型参数，JSON 格式。例如：`{"temperature":0.1}` |
| `MaxWordCount` | 否 | `384` | 每批翻译的原文最大 Token 数（估算值）。 |
| `MaxBatchTexts` | 否 | `10` | 每批翻译的最大原文条数。 |
| `ParallelCount` | 否 | `3` | 并行翻译任务的最大数量。 |
| `Interval` | 否 | `200` | 调度轮询间隔（毫秒）。间隔期间系统会合并多个翻译请求。 |
| `HalfWidth` | 否 | `true` | 是否将全角字符转换为半角。字体无法显示全角符号时建议开启。 |
| `MaxRetry` | 否 | `10` | 单条翻译失败后的最大重试次数。 |
| `SkipLatinOnly` | 否 | `false` | 是否跳过纯 ASCII 文本（如英文菜单）不做翻译直接原文返回。 |
| `LogLevel` | 否 | `Info` | 日志级别。可选：`Debug`、`Info`、`Warning`、`Error`。 |
| `DisableSpamChecks` | 否 | `false` | 是否禁用 AutoTranslator 的垃圾检查。 |

#### 上下文配置 / Context Configuration

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `RecentContextSize` | `10` | 保留的最近翻译条数（当前场景上下文）。 |
| `HistoryContextSize` | `20` | 保留的历史翻译条数（时间线上下文）。 |
| `MaxContextLength` | `3000` | 提示词中上下文部分的最大字符长度。 |
| `EnableSpeakerContext` | `true` | 是否启用角色说话风格检测与注入。 |

#### 验证检查配置 / Validation Configuration

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `CheckOriginalReturn` | `true` | 检查译文是否与原文完全相同（日文假名文本必须变化）。 |
| `CheckTranslationResidue` | `true` | 检查译文中是否残留源语言字符（如日文假名）。 |
| `CheckTagCount` | `true` | 检查原文与译文的标签数量（`<...>`、`[...]`、`%s` 等）是否一致。 |
| `CheckTargetLanguage` | `true` | 检查译文是否包含目标语言字符（如中文）。 |

### 完整配置示例 / Full Example

```ini
[AutoLLM]
APIKey=<KEY>
Model=qwen-turbo
URL=https://dashscope.aliyuncs.com/compatible-mode/v1
Requirement=/no_think
Terminology=
GameName=Death Must Die
GameDesc=一个刷装备打怪的游戏、暗黑破坏神的风格和元素
ModelParams={"temperature":0.1}
HalfWidth=True
MaxWordCount=384
MaxBatchTexts=10
ParallelCount=3
Interval=200
LogLevel=Info
MaxRetry=10
RecentContextSize=10
HistoryContextSize=20
MaxContextLength=3000
EnableSpeakerContext=True
CheckOriginalReturn=True
CheckTranslationResidue=True
CheckTagCount=True
CheckTargetLanguage=True
```

### 最小配置示例 / Minimal Example

```ini
[AutoLLM]
APIKey=
Model=qwen3:4b
URL=http://localhost:11434/v1
```

---

## 本地 LLM 服务器 / Local LLM Server

除了使用远程 LLM 服务外，也可以使用 [Ollama](https://ollama.com/) 等本地服务：
- 只需填写 `Model` 和 `URL`
- 本地运行时 `APIKey` 可留空
- 推荐模型尺寸：**8B**（质量与速度平衡），最低可用 **4B**

---

## 项目结构 / Project Structure

```
AutoLLMTranslator/
├── Dictionary/          # 游戏专用字典（JSON/TXT）
├── FuzzyString/         # 内嵌的模糊字符串匹配库
├── Models/              # 任务数据模型
├── Parsers/             # 请求构建与响应解析
├── Services/            # 调度器、文本后处理等
├── packages/            # XUnity 本地依赖 DLL
├── AutoLLMTranslatorEndpoint.cs    # XUnity 端点入口
├── Config.cs            # 默认提示词模板
├── ContextManager.cs    # 上下文管理
├── ContextPersistence.cs# 上下文持久化
├── DictionaryManager.cs # 字典管理
├── FileManager.cs       # 文件操作
├── HttpServer.cs        # 内部 HTTP 服务器
├── LLMClient.cs         # LLM API 调用客户端
├── PromptManager.cs     # 提示词管理
├── TaskManager.cs       # 翻译任务队列
├── TokenEstimator.cs    # Token 估算器
├── TranslationConfiguration.cs    # 配置定义
├── TranslationDatabase.cs         # 翻译数据库
├── TranslationProcessor.cs        # 翻译处理器
├── TranslationScheduler.cs        # 调度器
├── TranslationValidator.cs        # 结果验证器
└── ...
```

---

## 可能的问题 / Possible Issues

### 无法翻译 / 翻译异常

1. **检查 AutoTranslator 是否正确运行**。AutoTranslator 目前不支持 IL2CPP 类型游戏的插件运行。
2. **检查 LLM 服务配置**是否正确且可访问。
3. **确保 20000 端口没有被占用**。可在游戏运行时访问 [http://127.0.0.1:20000](http://127.0.0.1:20000) 确认。
4. **是否使用了足够强大的模型**。
5. **缺少 `Newtonsoft.Json.dll` 或版本不兼容**：
   - 下载兼容版本放到游戏的 `Managed` 目录下：[Newtonsoft.Json.dll](https://github.com/NothingNullNull/XUnity.AutoLLMTranslator/releases/download/2025%2F5%2F3/Newtonsoft.Json.dll)
6. **LLM 的 URL 和模型名字是否填写正确**：
   - ✅ 正确：`http(s)://XXXXXXX/v1`、`http(s)://XXXXXXX/v1/chat/completions`
   - ❌ 错误：`http(s)://XXXXXXX/v3`、`http(s)://XXXXXXX/`

### 翻译很慢

1. 是否使用了过于巨大的模型，建议 **8B** 左右。
2. 是否触发了 LLM 供应商的速率限制（QPM、TPM 等），适当调整 `ParallelCount` 和 `Interval`。
3. 本地 LLM 服务器是否有足够的硬件支持（GPU 显存、内存）。
4. 使用了 think 模型。大部分情况下不需要思考过程，qwen3 可使用 `/no_think` 关闭。

### 插件被自动关闭

虽然插件会尽力避免失败，但如果短时间内失败次数过多，AutoTranslator 会自动关闭该插件。此时需要重启游戏。

---

## 许可证 / License

本项目根据包含的许可证文件中规定的条款授权。详见 [LICENSE.txt](LICENSE.txt)。

## 致谢 / Acknowledgements

- **原作者**：[NothingNullNull/XUnity.AutoLLMTranslator](https://github.com/NothingNullNull/XUnity.AutoLLMTranslator) —— 感谢原作者的基础实现和开源贡献
- **框架**：基于 [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) 开发
- **库**：使用 [FuzzyString](https://github.com/kdjones/fuzzystring) 实现文本模糊搜索
- **库**：使用 [Newtonsoft.Json](https://www.newtonsoft.com/json) 进行 JSON 处理
