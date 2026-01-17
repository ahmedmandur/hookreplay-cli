# HookReplay CLI

Debug webhooks locally. Capture, inspect, and replay webhooks to your local development server.

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

## Links

- **Website:** https://hookreplay.dev

## License

MIT
