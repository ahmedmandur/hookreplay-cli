# HookReplay CLI

**Capture once. Replay forever.**

Debug webhooks locally. Capture, inspect, and replay webhooks to your local development server.

[![npm version](https://img.shields.io/npm/v/hookreplay?style=flat-square&color=black)](https://www.npmjs.com/package/hookreplay)
[![nuget version](https://img.shields.io/nuget/v/HookReplay.Cli?style=flat-square&color=black)](https://www.nuget.org/packages/HookReplay.Cli)
[![license](https://img.shields.io/badge/license-MIT-black?style=flat-square)](https://github.com/ahmedmandur/hookreplay-cli/blob/main/LICENSE)

## Installation

```bash
npm install -g hookreplay
```

### Alternative Installation

```bash
# .NET Tool (requires .NET 9+)
dotnet tool install --global HookReplay.Cli
```

## Quick Start

1. **Get an API key** from [hookreplay.dev/settings/api-keys](https://hookreplay.dev/settings/api-keys)

2. **Configure and connect:**
   ```bash
   hookreplay
   > config api-key YOUR_API_KEY
   > connect
   ```

3. **Receive webhooks** - Click "Send to CLI" in the web interface to forward requests to your local server.

## Commands

| Command | Description |
|---------|-------------|
| `help` | Show available commands |
| `config api-key <key>` | Set your API key |
| `config server <url>` | Set server URL |
| `connect` | Connect and wait for replays |
| `disconnect` | Disconnect from server |
| `status` | Show connection status |
| `history` | Show received requests |
| `replay <id> [url]` | Replay a request |
| `version` | Show version info |
| `update` | Check for and install updates |
| `quit` | Exit the CLI |

## How It Works

```
Webhook Provider → HookReplay → CLI → localhost:3000
```

1. Create a webhook endpoint at [hookreplay.dev](https://hookreplay.dev)
2. Point your webhook provider to your HookReplay URL
3. Webhooks are captured and stored
4. Connect CLI and click "Send to CLI" to forward to localhost
5. Debug with your IDE's breakpoints

## Changelog

### v1.0.18 (2026-01-23)

#### 🐛 Bug Fixes
- **Fixed response body display**: HTML/XML responses with `[` and `]` characters no longer crash the CLI
- **Fixed markup escaping**: All response content is now properly escaped

#### ✨ New Features
- **Auto-reconnect**: CLI automatically reconnects when the connection drops
  - Exponential backoff: 1s → 2s → 5s → 10s → 30s
  - Up to 10 reconnection attempts
  - Manual disconnect disables auto-reconnect

### v1.0.17

- Self-update feature: CLI can update itself
- Version checking on startup

## Contributing

We welcome contributions! See [CONTRIBUTING.md](https://github.com/ahmedmandur/hookreplay-cli/blob/main/CONTRIBUTING.md) for guidelines.

```bash
# Clone the repo
git clone https://github.com/ahmedmandur/hookreplay-cli.git
cd hookreplay-cli

# Build
dotnet build

# Run
dotnet run --project src/HookReplay.Cli
```

## Links

- **Website:** https://hookreplay.dev
- **Documentation:** https://hookreplay.dev/docs
- **GitHub:** https://github.com/ahmedmandur/hookreplay-cli

## License

MIT
