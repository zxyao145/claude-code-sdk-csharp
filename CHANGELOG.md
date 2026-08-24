## [10.3.2] - 2026-08-07

### 🚀 Features

- *(maf)* Add name metadata to claude code agent

## [10.3.1] - 2026-07-27

### ⚙️ Miscellaneous Tasks

- Upgrade nuget package

## [10.3.0] - 2026-07-16

### 🚀 Features

- *(maf)* Surface error content from results, retries, and tools

### 🐛 Bug Fixes

- *(maf)* Align RunCoreAsync content source with streaming variant

### ⚙️ Miscellaneous Tasks

- Update ci

## [10.2.0] - 2026-07-01

### 🚜 Refactor

- Upgrade Microsoft.Agents.AI to 1.12.0

## [10.1.0] - 2026-05-02

### 🚜 Refactor

- Update package versions for AI, test SDK, and coverlet
- Add logging, improve error handling, and enrich messages

## [10.0.0] - 2026-04-04

### 🚜 Refactor

- Upgrate Microsoft.Agents.AI to release, and keep the version consistent with .net

## [0.10.4] - 2026-03-18

### 🚀 Features

- Add chat history provider for ClaudeCodeAIAgent (#15)

### 🐛 Bug Fixes

- Some error
- Drain pending stream output after interrupt in ClaudeCodeSdk (#11)
- Ci
- Ci
- Claude code continuing the session cannot simply specify the session ID (#14)
- Error proxy message to claude

### 🚜 Refactor

- Add ConnectStatus tracking and improve error handling (#10)
- Upgrade MAF to 1.0.0-rc1

### 📚 Documentation

- Update README for clarity and versioning details
- Update README.md

### ⚙️ Miscellaneous Tasks

- Code clean
- Remove unused package in Microsoft.Extensions.AI in ClaudeCodeSdk.MAF

## [0.10.1] - 2026-01-31

### 🚀 Features

- Upgrade the ClaudeCodeSdk version and optimize dependencies and project structure
- Add ClaudeSdkClientManager for improved session lifecycle management

### 🐛 Bug Fixes

- Some bugs
- ClaudeCodeAIAgent
- SessionId not null when first query
- ANTHROPIC_AUTH_TOKEN environment variables not work
- Tool use result
- MessageParser bug

### 💼 Other

- Upgrade version

### 🚜 Refactor

- ManagePackageVersionsCentrally
- Simplify architecture by unifying core process layer (checkpoint)
- Change tpye to strong type MessageType
- The AIAgent configuration and message processing to enhance error support
- Enhance tool result handling and add cost tracking
- Disable GeneratePackageOnBuild
- Upgrade to MAF 1.0.0-preview.260121.1 (breaking change)

### 📚 Documentation

- Regenerate ClaudeCodeSdk.MAF README with comprehensive documentation
- Add comprehensive README for ClaudeCodeSdk package
- Enhance README with comprehensive documentation
- Enhance ClaudeCodeSdk README with comprehensive documentation

### ⚡ Performance

- Optimize ClaudeProcess for better performance and clarity

### ⚙️ Miscellaneous Tasks

- Add log
- Dotnet fotmat
- Checkpoint - bug fixes and type system enhancement
- Update publish.yml
- Add environment configuration to publish job

