# RiddleSharp

RiddleSharp 是 **Riddle** 语言的 C# 实现。Riddle 是一门静态类型、面向对象的语言，语法与类型系统与 C++ 等语言相近，并在类型推断、内存安全与可组合性等方面进行改进与简化。项目后续会使用 Riddle 进行自举（self-hosting）。

> 文档仍在编写中；你也可以从单元测试中了解语言与工具链的用法。

## 状态

- **开发阶段**：Alpha（API/语义可能变更）
- **平台**：Windows / macOS / Linux（.NET 支持的主流架构）
- **需要的环境**：.NET SDK **9.0** 或更高

## 特色（目标）

- 静态类型与类/接口（面向对象）
- 现代化泛型与类型推断
- 明确的内存与错误处理模型
- 简洁的构建链路与可读的诊断信息

> 上述为目标特性，以实际实现为准；欢迎在 Issue 中讨论/跟踪进展。

## 快速开始

```bash
# 1) 克隆仓库
git clone https://github.com/zi2ven/RiddleSharp
cd RiddleSharp

# 2) 构建
dotnet build

# 3) 运行测试以了解用法
dotnet test
```

> 当文档完成后，将提供 CLI 与语言示例。当前可参考 `tests/` 目录了解语法与工具链交互。

## 路线图 / Roadmap

- [ ] 编译到二进制（Binary）
- [ ] LSP（Language Server Protocol）
- [ ] 稳定 ABI
- [ ] FFI（外部函数接口，**实验性 / 不稳定**）

## 设计理念

- **可读性优先**：错误信息与诊断清晰直达
- **工程友好**：模块化、可测试、可集成
- **务实现代**：在熟悉的语法上引入现代特性，而非重新发明轮子

## 参与贡献

欢迎提交 Issue 与 PR：
- 报告 bug / 需求 / 设计建议
- 补充单元测试与文档
- 路线图条目实现

请遵循常见的 GitHub 工作流（分支、PR、代码审查）。

## 许可证

（在此注明你的许可证，例如 MIT / Apache-2.0）

## 致谢

感谢所有为 Riddle 与 RiddleSharp 提出想法、提交代码与反馈的贡献者。
