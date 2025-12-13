# 贡献指南

感谢您考虑为 MultiFlash Tool 做出贡献！

## 行为准则

- 尊重所有贡献者
- 保持友好和建设性的讨论
- 禁止任何形式的骚扰

## 如何贡献

### 报告 Bug

1. 检查是否已有相同的 Issue
2. 创建新 Issue，包含：
   - 问题描述
   - 复现步骤
   - 预期行为 vs 实际行为
   - 系统环境信息

### 提交功能请求

1. 创建 Issue 描述功能需求
2. 说明使用场景和预期效果
3. 等待社区讨论

### 提交代码

1. Fork 项目
2. 创建功能分支: `git checkout -b feature/your-feature`
3. 遵循代码规范 (参见 `.editorconfig`)
4. 提交更改: `git commit -m 'Add: 功能描述'`
5. 推送分支: `git push origin feature/your-feature`
6. 创建 Pull Request

## 代码规范

### 提交信息格式

```
类型: 简短描述

详细描述（可选）
```

类型：
- `Add:` 新功能
- `Fix:` Bug 修复
- `Refactor:` 重构
- `Docs:` 文档更新
- `Style:` 代码格式

### 代码风格

- 遵循 `.editorconfig` 配置
- 使用 4 空格缩进
- 添加必要的注释
- 使用 `#region` 组织代码
- 异常处理使用 `SafeExecuteAsync`

### Pull Request 检查清单

- [ ] 代码可编译通过
- [ ] 遵循代码规范
- [ ] 添加必要的注释
- [ ] 更新相关文档
- [ ] 测试功能正常

## 许可证

提交代码即表示您同意将代码以 GPL-3.0 许可证开源。
