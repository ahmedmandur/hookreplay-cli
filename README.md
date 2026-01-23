# HookReplay CLI

**Capture once. Replay forever.**

Debug webhooks on localhost without the tunnel pain.

---

[![build status](https://img.shields.io/github/actions/workflow/status/ahmedmandur/hookreplay-cli/ci.yml?branch=main&style=flat-square&label=build)](https://github.com/ahmedmandur/hookreplay-cli/actions/workflows/ci.yml)
[![npm version](https://img.shields.io/npm/v/hookreplay?style=flat-square&color=black)](https://www.npmjs.com/package/hookreplay)
[![nuget version](https://img.shields.io/nuget/v/HookReplay.Cli?style=flat-square&color=black)](https://www.nuget.org/packages/HookReplay.Cli)
[![license](https://img.shields.io/badge/license-MIT-black?style=flat-square)](https://github.com/ahmedmandur/hookreplay-cli/blob/main/LICENSE)

[Website](https://hookreplay.dev) • [Documentation](https://hookreplay.dev/docs) • [GitHub](https://github.com/ahmedmandur/hookreplay-cli) • [Twitter](https://twitter.com/hookreplaydev)

---

## The Problem

Every developer has been there:

1. A Stripe webhook fails in production
2. You need to reproduce the exact payload locally
3. You set up ngrok, trigger test webhooks from Stripe's dashboard
4. Repeat 47 times until you figure out the bug
5. **3 hours later**, you're exhausted

## The Solution

**HookReplay** captures webhooks and lets you replay them to localhost — as many times as you need.

```
Capture once → Replay forever
```

No tunnels. No re-triggering. No wasted hours.

---

## Installation

### npm (recommended)

```bash
npm install -g hookreplay
```

### .NET Tool

```bash
dotnet tool install -g HookReplay.Cli
```

---

## Quick Start

### 1. Get your API key

Sign up at [hookreplay.dev](https://hookreplay.dev) and grab your API key from Settings.

### 2. Configure the CLI

```bash
hookreplay
> config api-key YOUR_API_KEY
```

### 3. Connect and receive webhooks

```bash
> connect
✓ Connected to HookReplay server
Waiting for replay requests...
```

### 4. Replay webhooks from the web dashboard

Click "Send to CLI" on any captured webhook, and it will be forwarded to your localhost with your breakpoints ready.

---

## Commands

| Command | Description |
|---------|-------------|
| `help` | Show available commands |
| `config api-key <key>` | Set your API key |
| `config server <url>` | Set server URL (default: https://hookreplay.dev) |
| `connect` | Connect to HookReplay and wait for replays |
| `disconnect` | Disconnect from server |
| `status` | Show connection status |
| `history` | Show received requests |
| `replay <id> [url]` | Replay a request from history to a different URL |
| `version` | Show version and system info |
| `update` | Check for and install updates |
| `clear` | Clear the screen |
| `quit` | Exit the CLI |

---

## Self-Update

The CLI can update itself without requiring npm or dotnet commands:

```
● hookreplay> update
Downloading v1.0.16...
✓ Updated to v1.0.16!
Please restart the CLI to use the new version.
```

**How it works:**
- On startup, the CLI checks for new versions from npm registry
- When you run `update`, it downloads the new binary directly from [GitHub Releases](https://github.com/ahmedmandur/hookreplay-cli/releases)
- The update works even while the CLI is running (no need to exit first)
- After updating, restart the CLI to use the new version

You can also update manually:
- **npm**: `npm install -g hookreplay`
- **dotnet**: `dotnet tool update -g HookReplay.Cli`

---

## How It Works

```
┌─────────────┐      ┌─────────────────┐      ┌─────────────────┐
│   Stripe    │ ──▶  │   HookReplay    │ ──▶  │   Your CLI      │
│   Webhook   │      │   (captures)    │      │   (replays)     │
└─────────────┘      └─────────────────┘      └─────────────────┘
                                                      │
                                                      ▼
                                              ┌─────────────────┐
                                              │  localhost:3000 │
                                              │  (your server)  │
                                              └─────────────────┘
```

1. Create a webhook endpoint at [hookreplay.dev](https://hookreplay.dev)
2. Point your webhook provider (Stripe, Shopify, GitHub, etc.) to your HookReplay URL
3. Webhooks are captured and stored
4. Connect the CLI and click "Send to CLI" to replay requests to localhost
5. Debug with your IDE's breakpoints

---

## Why HookReplay?

| Problem | HookReplay Solution |
|---------|---------------------|
| "I can't reproduce the exact payload" | Capture it once, replay it 100 times |
| "I keep re-triggering webhooks from Stripe" | Replay from history instead |
| "My ngrok tunnel expired again" | No tunnels needed |
| "I need to test edge cases" | Edit the payload before replaying |
| "I just need to see what arrived" | Inspect every header and byte |

---

## Configuration

Config is stored in `~/.hookreplay/config.json`:

```json
{
  "apiKey": "hr_abc123...",
  "serverUrl": "https://hookreplay.dev"
}
```

---

## Telemetry

The CLI sends anonymous, minimal telemetry on first run to help us understand usage:

- Install ID (random, not linked to you)
- CLI version
- OS and architecture
- .NET version

No personal data, API keys, or webhook content is ever sent. You can see exactly what's collected in the source code.

---

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development

```bash
# Clone the repo
git clone https://github.com/hookreplay/cli.git
cd cli

# Build
dotnet build

# Run
dotnet run
```

---

## Links

- **Website**: [hookreplay.dev](https://hookreplay.dev)
- **Documentation**: [hookreplay.dev/docs](https://hookreplay.dev/docs)
- **Twitter**: [@hookreplaydev](https://twitter.com/hookreplaydev)
- **Issues**: [GitHub Issues](https://github.com/ahmedmandur/hookreplay-cli/issues)

---

## License

MIT License - see [LICENSE](LICENSE) for details.

---

## Changelog

### v1.0.18 (2026-01-23)

#### 🐛 Bug Fixes
- **Fixed response body display**: HTML/XML responses with `[` and `]` characters no longer crash the CLI with "malformed markup tag" errors
- **Fixed Spectre.Console markup escaping**: All user-generated content is now properly escaped before rendering

#### ✨ New Features
- **Auto-reconnect**: CLI now automatically reconnects when the connection drops unexpectedly
  - Uses exponential backoff: 1s → 2s → 5s → 10s → 30s delays
  - Attempts up to 10 reconnections before giving up
  - Manual disconnect (`disconnect` command or Ctrl+C) disables auto-reconnect
  - Shows clear status messages during reconnection attempts

#### 🔧 Improvements
- Better error messages with escaped content to prevent display issues
- Connection state is properly tracked across reconnection attempts

---

### v1.0.17

- Self-update feature: CLI can update itself without npm/dotnet commands
- Version checking against npm registry on startup
- Direct binary download from GitHub Releases

---

**Stop chasing webhooks. Start catching bugs.**

[Get started free →](https://hookreplay.dev)
